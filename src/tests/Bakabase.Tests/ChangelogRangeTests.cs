using System.Collections.Generic;
using System.Linq;
using Bakabase.Service.Components.Changelog;
using Bakabase.Service.Models.View;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Bakabase.Tests;

/// <summary>
/// The span a user crosses when they take one update. Ordering is delegated to the
/// Semver package, so what is pinned here is the selection policy around it — and the
/// one SemVer rule (§11.4.2) whose correct behaviour looks like a bug.
/// </summary>
[TestClass]
public class ChangelogRangeTests
{
    /// <summary>
    /// Deliberately NOT sorted by version: the real archive is ordered by publish date,
    /// and this project's history proves the two disagree (v2.0.5 was published two
    /// minutes after v2.1.7-beta). BuildRange must not inherit this order.
    /// </summary>
    private static ChangelogIndexViewModel Index(params string[] versions) => new()
    {
        Releases = versions.Select(v => new ChangelogReleaseViewModel
        {
            Version = v,
            Tag = $"v{v}",
            Prerelease = v.Contains('-')
        }).ToList()
    };

    private static List<string> VersionsOf(ChangelogRangeViewModel? range) =>
        range!.Releases.Select(r => r.Version).ToList();

    [TestMethod]
    public void OrdersDotSeparatedPreReleasesNumerically()
    {
        // The trap a hand-rolled string compare falls into: "142" < "75" lexically, but
        // a dot-separated all-digit identifier is compared as a number.
        var range = ChangelogRangeTests.BuildBeta("2.4.0-beta.2",
            "2.4.0-beta.142",
            "2.4.0-beta.75", "2.4.0-beta.142", "2.4.0-beta.18");

        CollectionAssert.AreEqual(
            new[] {"2.4.0-beta.142", "2.4.0-beta.75", "2.4.0-beta.18"},
            VersionsOf(range));
    }

    private static ChangelogRangeViewModel? BuildBeta(string from, string to, params string[] indexVersions) =>
        ChangelogService.BuildRange(Index(indexVersions), from, to);

    [TestMethod]
    public void ExcludesTheLowerBoundAndIncludesTheUpper()
    {
        var range = BuildBeta("2.4.0-beta.18", "2.4.0-beta.75",
            "2.4.0-beta.18", "2.4.0-beta.25", "2.4.0-beta.75", "2.4.0-beta.142");

        CollectionAssert.AreEqual(new[] {"2.4.0-beta.75", "2.4.0-beta.25"}, VersionsOf(range));
    }

    /// <summary>
    /// SemVer 2.0.0 §11.4.2: a prerelease label with no dot is ONE alphanumeric
    /// identifier, compared ASCII-lexically — so <c>beta10</c> sorts BELOW <c>beta9</c>.
    /// v1.9.0-beta10 is a real tag in this repository. The consequence is that it falls
    /// outside a span anchored at beta2..beta9, and that is correct rather than a defect:
    /// the alternative is a natural-sort that disagrees with the ordering the rest of the
    /// application (AppHost migrations, the updater) already uses. Pinned so nobody
    /// "fixes" it into a second version semantics.
    /// </summary>
    [TestMethod]
    public void NonDottedPreReleaseLabelsCompareAsciiNotNumerically()
    {
        var range = BuildBeta("1.9.0-beta2", "1.9.0-beta9",
            "1.9.0-beta2", "1.9.0-beta3", "1.9.0-beta9", "1.9.0-beta10");

        CollectionAssert.AreEqual(new[] {"1.9.0-beta9", "1.9.0-beta3"}, VersionsOf(range));
        Assert.IsFalse(VersionsOf(range).Contains("1.9.0-beta10"),
            "beta10 is ASCII-below beta9 and must stay out of this span");
    }

    /// <summary>
    /// A release outranks every prerelease of the same version, so 1.9.0 sits ABOVE an
    /// upper bound of 1.9.0-rc3 and must not be pulled into the span — the user has not
    /// been offered it. Within the prereleases, ordering is ASCII on the label.
    /// </summary>
    [TestMethod]
    public void ReleaseOutranksItsOwnPreReleasesAndStaysOutOfTheirSpan()
    {
        var range = BuildBeta("1.9.0-beta9", "1.9.0-rc3",
            "1.9.0", "1.9.0-rc3", "1.9.0-rc", "1.9.0-beta9");

        CollectionAssert.AreEqual(new[] {"1.9.0-rc3", "1.9.0-rc"}, VersionsOf(range));
    }

    /// <summary>The same boundary from the other side: 1.9.0 as the target includes itself.</summary>
    [TestMethod]
    public void ReleaseIsIncludedWhenItIsTheTarget()
    {
        var range = ChangelogService.BuildRange(
            Index("1.9.0", "1.9.0-rc3", "1.9.0-rc", "1.9.0-beta9"), "1.9.0-beta9", "1.9.0");

        CollectionAssert.AreEqual(new[] {"1.9.0"}, VersionsOf(range));
        Assert.AreEqual(2, range!.HiddenPrereleaseCount, "the two rc builds are hidden, not lost");
    }

    /// <summary>
    /// A stable target means a stable reader. A stable release's notes are generated from
    /// the previous STABLE release (_release.yml picks the previous tag that way), so the
    /// pre-releases in between are already covered by the one entry — dropping them loses
    /// no content. The count is still reported so a one-row list does not read as broken.
    /// </summary>
    [TestMethod]
    public void StableTargetHidesPreReleasesButReportsHowMany()
    {
        var range = ChangelogService.BuildRange(
            Index("2.3.0", "2.3.0-beta.296", "2.3.0-beta.69", "2.2.0"),
            "2.2.0", "2.3.0");

        CollectionAssert.AreEqual(new[] {"2.3.0"}, VersionsOf(range));
        Assert.AreEqual(2, range!.HiddenPrereleaseCount);
    }

    [TestMethod]
    public void PreReleaseTargetKeepsEverythingAndHidesNothing()
    {
        var range = BuildBeta("2.3.0", "2.4.0-beta.18",
            "2.4.0-beta.18", "2.4.0-beta.2", "2.3.0");

        CollectionAssert.AreEqual(new[] {"2.4.0-beta.18", "2.4.0-beta.2"}, VersionsOf(range));
        Assert.AreEqual(0, range!.HiddenPrereleaseCount);
    }

    /// <summary>
    /// The index is cached for an hour and published by a different workflow step than the
    /// update feed, so the version being installed can be missing from it. Losing the
    /// headline row is the one outcome this list must never have.
    /// </summary>
    [TestMethod]
    public void SynthesizesTheTargetWhenTheIndexHasNotCaughtUp()
    {
        var range = BuildBeta("2.4.0-beta.18", "2.4.0-beta.142",
            "2.4.0-beta.18", "2.4.0-beta.25");

        CollectionAssert.AreEqual(new[] {"2.4.0-beta.142", "2.4.0-beta.25"}, VersionsOf(range));

        var target = range!.Releases.First();
        Assert.AreEqual("v2.4.0-beta.142", target.Tag);
        Assert.IsTrue(target.Prerelease);
        Assert.IsTrue(target.HtmlUrl!.EndsWith("/tag/v2.4.0-beta.142"));
    }

    [TestMethod]
    public void SynthesizedTargetDoesNotDisturbTheHiddenCount()
    {
        var range = ChangelogService.BuildRange(
            Index("2.3.0-beta.296", "2.3.0-beta.69"), "2.2.0", "2.3.0");

        CollectionAssert.AreEqual(new[] {"2.3.0"}, VersionsOf(range));
        Assert.AreEqual(2, range!.HiddenPrereleaseCount);
    }

    /// <summary>
    /// AppInfo.CoreVersion and AppVersionInfo.RunningVersion are SemVersion.ToString(),
    /// which keeps build metadata; archive entries never carry it. The server resolves the
    /// bounds and echoes them so the client renders and fetches against those.
    /// </summary>
    [TestMethod]
    public void StripsBuildMetadataFromBothBoundsAndEchoesTheResolvedOnes()
    {
        var range = BuildBeta("2.4.0-beta.18+abc123", "2.4.0-beta.75+deadbeef",
            "2.4.0-beta.18", "2.4.0-beta.25", "2.4.0-beta.75");

        Assert.AreEqual("2.4.0-beta.18", range!.From);
        Assert.AreEqual("2.4.0-beta.75", range.To);
        CollectionAssert.AreEqual(new[] {"2.4.0-beta.75", "2.4.0-beta.25"}, VersionsOf(range));
    }

    /// <summary>
    /// Every failure returns null so the caller collapses to the single-version modal. The
    /// outcome this must never have is a bound that stops being a bound: answering a
    /// "what's new" question with the entire archive.
    /// </summary>
    [DataTestMethod]
    [DataRow(null, "2.3.0", DisplayName = "null lower bound")]
    [DataRow("", "2.3.0", DisplayName = "empty lower bound")]
    [DataRow("not-a-version!", "2.3.0", DisplayName = "lower bound outside the version alphabet")]
    [DataRow("garbage", "2.3.0", DisplayName = "lower bound unparseable as semver")]
    [DataRow("2.2.0", null, DisplayName = "null upper bound")]
    [DataRow("2.2.0", "nonsense", DisplayName = "upper bound unparseable")]
    [DataRow("2.3.0", "2.3.0", DisplayName = "already on the target")]
    [DataRow("2.4.0", "2.3.0", DisplayName = "bounds reversed")]
    [DataRow("2.3.0+meta", "2.3.0", DisplayName = "equal once metadata is stripped")]
    public void FailsClosedRatherThanWidening(string? from, string? to)
    {
        Assert.IsNull(ChangelogService.BuildRange(
            Index("2.4.0-beta.142", "2.3.0", "2.2.0", "2.1.17"), from, to));
    }

    [TestMethod]
    public void SkipsIndexEntriesItCannotPlace()
    {
        var range = ChangelogService.BuildRange(
            Index("2.3.0", "not-a-version", "", "2.2.5", "2.2.0"), "2.2.0", "2.3.0");

        CollectionAssert.AreEqual(new[] {"2.3.0", "2.2.5"}, VersionsOf(range));
    }

    /// <summary>
    /// v2.0.5 was published two minutes AFTER v2.1.7-beta, so the archive's publish-date
    /// order is not version order. The span must be built on precedence.
    /// </summary>
    [TestMethod]
    public void OrdersByPrecedenceNotByTheIndexOrder()
    {
        var range = BuildBeta("2.0.4", "2.1.7-beta",
            "2.1.7-beta", "2.0.5", "2.1.6-beta", "2.0.4");

        CollectionAssert.AreEqual(new[] {"2.1.7-beta", "2.1.6-beta", "2.0.5"}, VersionsOf(range));
    }

    [TestMethod]
    public void ReportsTheGithubFallbackUrl()
    {
        var range = BuildBeta("2.4.0-beta.18", "2.4.0-beta.75", "2.4.0-beta.75");

        Assert.AreEqual(ChangelogService.ReleasesUrl, range!.ReleasesUrl);
    }
}
