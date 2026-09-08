using Bakabase.Service.Components.Changelog;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Bakabase.Tests;

/// <summary>
/// The version a client asks for ends up inside the archive URL, so anything
/// outside the SemVer alphabet is a probe at another CDN path rather than a
/// release. Every shape the pipeline has ever published must still pass.
/// </summary>
[TestClass]
public class ChangelogServiceTests
{
    [DataTestMethod]
    [DataRow("2.4.0-beta.142")]
    [DataRow("2.3.0")]
    [DataRow("2.1.15-beta")]
    [DataRow("1.9.0-rc3")]
    [DataRow("2.0.1")]
    [DataRow("1.0.0+build.5")]
    public void AcceptsPublishedVersions(string version)
    {
        Assert.IsTrue(ChangelogService.IsSupportedVersion(version), version);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("../../../etc/passwd")]
    [DataRow("2.3.0/../../secret")]
    [DataRow("2.3.0?x=1")]
    [DataRow("2.3.0#frag")]
    [DataRow("https://evil.example.com/a")]
    [DataRow("//evil.example.com")]
    [DataRow("2.3.0 2.4.0")]
    [DataRow(".2.3.0")]
    [DataRow("-2.3.0")]
    public void RejectsAnythingElse(string? version)
    {
        Assert.IsFalse(ChangelogService.IsSupportedVersion(version), version ?? "<null>");
    }

    [TestMethod]
    public void RejectsOverlongVersions()
    {
        Assert.IsFalse(ChangelogService.IsSupportedVersion(new string('1', 65)));
        Assert.IsTrue(ChangelogService.IsSupportedVersion(new string('1', 64)));
    }
}
