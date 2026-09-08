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

    /// <summary>One release's notes. Null data means they could not be fetched.</summary>
    public record ChangelogViewModel
    {
        public string Version { get; set; } = null!;

        /// <summary>Markdown, byte-for-byte what the GitHub release body says.</summary>
        public string Markdown { get; set; } = null!;

        public string? HtmlUrl { get; set; }
    }
}
