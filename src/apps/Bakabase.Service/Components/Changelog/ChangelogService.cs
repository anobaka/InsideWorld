using System;
using System.Collections.Concurrent;
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
        private readonly ConcurrentDictionary<string, string> _bodies = new();

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

        private async Task<string?> GetMarkdownAsync(string version, CancellationToken ct)
        {
            if (_bodies.TryGetValue(version, out var cached))
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

                if (_bodies.Count >= MaxCachedBodies)
                {
                    _bodies.Clear();
                }

                _bodies[version] = markdown;
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
