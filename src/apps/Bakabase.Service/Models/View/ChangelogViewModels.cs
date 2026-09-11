using System;
using System.Collections.Generic;

namespace Bakabase.Service.Models.View
{
    /// <summary>
    /// The published release history, as CI mirrored it from GitHub Releases
    /// (see scripts/changelog/build_changelog_archive.py). Null data on the
    /// endpoint means the archive could not be reached — an offline host, or a
    /// blocked CDN — and the UI then offers <see cref="ReleasesUrl"/> instead.
    /// </summary>
    public record ChangelogIndexViewModel
    {
        public DateTime? GeneratedAt { get; set; }

        /// <summary>Newest first.</summary>
        public List<ChangelogReleaseViewModel> Releases { get; set; } = [];

        /// <summary>The same notes on GitHub, for readers who want the source.</summary>
        public string? ReleasesUrl { get; set; }
    }

    public record ChangelogReleaseViewModel
    {
        /// <summary>Bare version, no <c>v</c> prefix — e.g. <c>2.4.0-beta.142</c>.</summary>
        public string Version { get; set; } = null!;

        public string? Tag { get; set; }

        public string? Name { get; set; }

        public bool Prerelease { get; set; }

        public DateTime? PublishedAt { get; set; }

        public string? HtmlUrl { get; set; }
    }

    /// <summary>
    /// Every release a user crosses when they take one update — what changed between
    /// the version they have and the version they are about to install.
    /// <para>
    /// Null data on the endpoint means the span could not be resolved (an unreadable
    /// bound, an unreachable archive, or a lower bound that is not actually lower).
    /// The caller then falls back to showing the target version's notes alone: this
    /// endpoint never widens a failure into "here is the entire history".
    /// </para>
    /// </summary>
    public record ChangelogRangeViewModel
    {
        /// <summary>
        /// The lower bound as the server resolved it — exclusive, and normalized, so
        /// build metadata a client sent (<c>2.3.0+abc123</c>) is gone. Render and fetch
        /// against this, never against what was sent.
        /// </summary>
        public string From { get; set; } = null!;

        /// <summary>The upper bound as resolved — inclusive, normalized.</summary>
        public string To { get; set; } = null!;

        /// <summary>Newest first, by SemVer precedence rather than publish date.</summary>
        public List<ChangelogReleaseViewModel> Releases { get; set; } = [];

        /// <summary>
        /// Pre-releases inside the span that a stable-channel reader is not shown. Their
        /// content is not lost — a stable release's notes are generated from the previous
        /// *stable* release, so they already cover every pre-release in between — but the
        /// count is surfaced so a one-row list does not read as a broken filter.
        /// </summary>
        public int HiddenPrereleaseCount { get; set; }

        /// <summary>The same notes on GitHub, for readers who want the source.</summary>
        public string? ReleasesUrl { get; set; }
    }

    /// <summary>One release's notes. Null data means they could not be fetched.</summary>
    public record ChangelogViewModel
    {
        public string Version { get; set; } = null!;

        /// <summary>Markdown, byte-for-byte what the GitHub release body says.</summary>
        public string Markdown { get; set; } = null!;

        public string? HtmlUrl { get; set; }
    }
}
