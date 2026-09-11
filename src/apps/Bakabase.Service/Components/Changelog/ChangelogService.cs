using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Bakabase.Infrastructures.Components.App.Upgrade.Abstractions;
using Bakabase.Service.Models.View;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Semver;

namespace Bakabase.Service.Components.Changelog
{
    /// <summary>
    /// Reads the release-notes archive CI publishes next to the update feed.
    /// <para>
    /// Deliberately not the GitHub API: unauthenticated calls are capped at 60/hour
    /// per IP — shared by everyone behind one NAT — and a large part of the user base
    /// cannot reach github.com reliably at all. The archive is a mirror rather than
    /// hand-maintained content, so it cannot drift from the releases it mirrors: each
    /// <c>README.md</c> is the same <c>CHANGELOG_{version}.md</c> artifact the release
    /// workflow hands GitHub as the release body, and <c>index.json</c> is rebuilt in
    /// full from the releases API on every release. See
    /// <c>scripts/changelog/build_changelog_archive.py</c> and <c>_release.yml</c>.
    /// </para>
    /// </summary>
    public class ChangelogService(IAppUpdateSource updateSource, ILogger<ChangelogService> logger)
    {
        /// <summary>Where a reader can compare against the authoritative copy.</summary>
        public const string ReleasesUrl = "https://github.com/anobaka/Bakabase/releases";

        private static readonly TimeSpan IndexCacheTtl = TimeSpan.FromHours(1);

        /// <summary>
        /// A published release's notes never change, so bodies are cached without a TTL —
        /// only bounded, so browsing the whole history cannot grow the process forever.
        /// </summary>
        private const int MaxCachedBodies = 64;

        /// <summary>
        /// Versions reach this service from the client and end up in a URL. Velopack
        /// versions are SemVer, so anything outside that alphabet — a slash, a dot
        /// segment — is a probe at another CDN path, not a version.
        /// </summary>
        private static readonly Regex VersionPattern = new(@"^[0-9A-Za-z][0-9A-Za-z.\-+]{0,63}$",
            RegexOptions.Compiled);

        /// <summary>
        /// Whether a client-supplied version may be turned into an archive URL.
        /// </summary>
        public static bool IsSupportedVersion(string? version) =>
            !string.IsNullOrWhiteSpace(version) && VersionPattern.IsMatch(version);

        private static readonly HttpClient Http = new() {Timeout = TimeSpan.FromSeconds(15)};

        private readonly SemaphoreSlim _indexLock = new(1, 1);

        // Least-recently-read eviction. Walking a multi-version span is now the primary
        // way this cache is used, so dropping everything on reaching the cap — which is
        // what a plain Clear() does — would re-fetch the whole span from the CDN as soon
        // as a reader crossed the boundary.
        private readonly object _bodiesLock = new();
        private readonly Dictionary<string, string> _bodies = new();
        private readonly LinkedList<string> _bodyOrder = new();

        private ChangelogIndexViewModel? _cachedIndex;
        private DateTime _indexFetchedAt = DateTime.MinValue;

        private string ArchiveBaseUrl => $"{updateSource.GetBaseUrl().TrimEnd('/')}/changelogs";

        /// <summary>
        /// The release history, or null when the archive cannot be reached and nothing
        /// is cached — the UI then explains instead of erroring.
        /// </summary>
        public async Task<ChangelogIndexViewModel?> GetIndexAsync(CancellationToken ct)
        {
            if (_cachedIndex != null && DateTime.UtcNow - _indexFetchedAt < IndexCacheTtl)
            {
                return _cachedIndex;
            }

            await _indexLock.WaitAsync(ct);
            try
            {
                if (_cachedIndex != null && DateTime.UtcNow - _indexFetchedAt < IndexCacheTtl)
                {
                    return _cachedIndex;
                }

                // The CDN caches the index object; a fresh query string asks it for the
                // origin's current version without needing a cache purge.
                var url = $"{ArchiveBaseUrl}/index.json?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                var json = await Http.GetStringAsync(url, ct);
                var index = JsonConvert.DeserializeObject<ChangelogIndexViewModel>(json);

                if (index?.Releases is {Count: > 0})
                {
                    index.ReleasesUrl = ReleasesUrl;
                    _cachedIndex = index;
                    _indexFetchedAt = DateTime.UtcNow;
                }

                return _cachedIndex;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // Offline host, blocked CDN, nothing published yet — all fine;
                // yesterday's cache (if any) keeps serving.
                logger.LogWarning(e, "Could not fetch the changelog index");
                return _cachedIndex;
            }
            finally
            {
                _indexLock.Release();
            }
        }

        /// <summary>
        /// Parses a version the way this project publishes them. Ordering is delegated to
        /// the Semver package rather than hand-rolled: the two prerelease shapes in this
        /// repo's history need opposite rules — <c>beta.142</c> is a dot-separated numeric
        /// identifier (142 &gt; 75 numerically) while <c>beta10</c> is a single alphanumeric
        /// identifier (ASCII, so <c>beta10</c> sorts *below* <c>beta9</c>, per SemVer
        /// 2.0.0 §11.4.2 — see ChangelogRangeTests, which pins that as deliberate).
        /// <para>
        /// Build metadata is dropped: the client's own version comes from
        /// <c>SemVersion.ToString()</c> and can carry <c>+meta</c>, while archive entries
        /// never do.
        /// </para>
        /// </summary>
        public static SemVersion? ParseVersion(string? version)
        {
            if (!IsSupportedVersion(version) ||
                !SemVersion.TryParse(version, SemVersionStyles.Any, out var parsed))
            {
                return null;
            }

            return parsed.WithoutMetadata();
        }

        /// <summary>
        /// The releases a user crosses going from <paramref name="from"/> (exclusive) to
        /// <paramref name="to"/> (inclusive), newest first — or null when the span cannot
        /// be resolved.
        /// <para>
        /// Every failure here returns null rather than a wider answer. An unreadable lower
        /// bound must not degrade to "no lower bound", which would answer a "what's new"
        /// question with the entire ~190-release archive.
        /// </para>
        /// </summary>
        public async Task<ChangelogRangeViewModel?> GetRangeAsync(string? from, string? to,
            CancellationToken ct)
        {
            var index = await GetIndexAsync(ct);

            return index == null ? null : BuildRange(index, from, to);
        }

        /// <summary>
        /// The pure half of <see cref="GetRangeAsync"/> — every rule about which versions
        /// belong in a span lives here so it can be tested without a network.
        /// </summary>
        public static ChangelogRangeViewModel? BuildRange(ChangelogIndexViewModel index, string? from,
            string? to)
        {
            var lower = ParseVersion(from);
            var upper = ParseVersion(to);

            if (lower == null || upper == null)
            {
                return null;
            }

            // Already current, or the bounds arrived backwards. Either way there is no
            // span to show, and the caller falls back to a single version.
            if (SemVersion.PrecedenceComparer.Compare(lower, upper) >= 0)
            {
                return null;
            }

            var placed = index.Releases
                .Select(r => (Release: r, Version: ParseVersion(r.Version)))
                // The index is external data; an entry we cannot place is skipped rather
                // than allowed to throw out of the endpoint.
                .Where(x => x.Version != null)
                .Select(x => (x.Release, Version: x.Version!))
                .ToList();

            var inSpan = placed
                .Where(x => SemVersion.PrecedenceComparer.Compare(x.Version, lower) > 0 &&
                            SemVersion.PrecedenceComparer.Compare(x.Version, upper) <= 0)
                .ToList();

            // A stable target means a stable reader, whose notes already aggregate every
            // pre-release in between (a stable release's changelog is generated from the
            // previous stable release — see _release.yml), so the betas are redundant here.
            var keepPreReleases = upper.IsPrerelease;
            var kept = keepPreReleases
                ? inSpan.ToList()
                : inSpan.Where(x => !x.Version.IsPrerelease).ToList();

            // Counted before the target row is forced in, or a synthesized target would
            // silently decrement the hidden count.
            var hiddenPreReleaseCount = inSpan.Count - kept.Count;

            // The index is cached for an hour and is published by a different workflow step
            // than the update feed, so the very version being installed can be missing from
            // it. Losing the headline row is the one outcome this list cannot have.
            if (kept.All(x => SemVersion.PrecedenceComparer.Compare(x.Version, upper) != 0))
            {
                var target = placed.FirstOrDefault(x =>
                    SemVersion.PrecedenceComparer.Compare(x.Version, upper) == 0);

                kept.Add(target.Release == null
                    ? (new ChangelogReleaseViewModel
                    {
                        Version = upper.ToString(),
                        Tag = $"v{upper}",
                        Name = upper.ToString(),
                        Prerelease = upper.IsPrerelease,
                        HtmlUrl = $"{ReleasesUrl}/tag/v{upper}"
                    }, upper)
                    : target);
            }

            return new ChangelogRangeViewModel
            {
                From = lower.ToString(),
                To = upper.ToString(),
                Releases = kept
                    .OrderByDescending(x => x.Version, SemVersion.PrecedenceComparer)
                    .Select(x => x.Release)
                    .ToList(),
                HiddenPrereleaseCount = hiddenPreReleaseCount,
                ReleasesUrl = ReleasesUrl
            };
        }

        /// <summary>
        /// One release's notes, or null when the version has no published notes or the
        /// archive cannot be reached.
        /// </summary>
        public async Task<ChangelogViewModel?> GetAsync(string? version, CancellationToken ct)
        {
            if (!IsSupportedVersion(version))
            {
                return null;
            }

            var markdown = await GetMarkdownAsync(version, ct);
            if (markdown == null)
            {
                return null;
            }

            return new ChangelogViewModel
            {
                Version = version,
                Markdown = markdown,
                HtmlUrl = await ResolveHtmlUrlAsync(version, ct)
            };
        }

        private bool TryReadBody(string version, out string? markdown)
        {
            lock (_bodiesLock)
            {
                if (!_bodies.TryGetValue(version, out markdown))
                {
                    return false;
                }

                _bodyOrder.Remove(version);
                _bodyOrder.AddLast(version);

                return true;
            }
        }

        private void StoreBody(string version, string markdown)
        {
            lock (_bodiesLock)
            {
                // Remove first so a re-read cannot leave the key queued twice, which would
                // let a later eviction drop an entry that is still in the dictionary.
                _bodyOrder.Remove(version);
                _bodyOrder.AddLast(version);
                _bodies[version] = markdown;

                while (_bodies.Count > MaxCachedBodies && _bodyOrder.First is { } oldest)
                {
                    _bodyOrder.RemoveFirst();
                    _bodies.Remove(oldest.Value);
                }
            }
        }

        private async Task<string?> GetMarkdownAsync(string version, CancellationToken ct)
        {
            if (TryReadBody(version, out var cached))
            {
                return cached;
            }

            try
            {
                var markdown = await Http.GetStringAsync($"{ArchiveBaseUrl}/{version}/README.md", ct);
                if (string.IsNullOrWhiteSpace(markdown))
                {
                    return null;
                }

                StoreBody(version, markdown);
                return markdown;
            }
            catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
                // Versions published before the archive existed, and local dev builds
                // that were never released, have no notes. Not worth an error.
                logger.LogInformation("No changelog published for {Version}", version);
                return null;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning(e, "Could not fetch the changelog of {Version}", version);
                return null;
            }
        }

        /// <summary>
        /// The GitHub release page for this version. Taken from the index when it is
        /// available; otherwise assembled from the tag convention, which is the same
        /// answer for every release the pipeline has ever produced.
        /// </summary>
        private async Task<string> ResolveHtmlUrlAsync(string version, CancellationToken ct)
        {
            var index = await GetIndexAsync(ct);
            var entry = index?.Releases.FirstOrDefault(r =>
                string.Equals(r.Version, version, StringComparison.OrdinalIgnoreCase));

            return entry?.HtmlUrl ?? $"{ReleasesUrl}/tag/v{version}";
        }
    }
}
