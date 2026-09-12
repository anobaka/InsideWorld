using System;
using System.Collections.Generic;

namespace Bakabase.Service.Models.View
{
    /// <summary>
    /// Everything that can be installed on a device other than this one.
    /// </summary>
    /// <remarks>
    /// One response rather than one per product: the page shows them together, and a
    /// reader deciding between a phone and a second computer is answering one question.
    /// Either half is null when its manifest could not be reached — an offline host, a
    /// blocked CDN, or a product with nothing published yet all look alike, and the page
    /// explains rather than erroring.
    /// </remarks>
    public record OtherDeviceDownloadsViewModel
    {
        public MobileAppDownloadsViewModel? Mobile { get; set; }

        public ClientAppDownloadsViewModel? DesktopClient { get; set; }
    }

    /// <summary>
    /// The latest published mobile packages, as CI recorded them in the
    /// download manifest.
    /// </summary>
    public record MobileAppDownloadsViewModel
    {
        public string Version { get; set; } = null!;

        public DateTime? PublishedAt { get; set; }

        /// <summary>The GitHub release these packages were published on.</summary>
        public string? ReleaseUrl { get; set; }

        /// <summary>The SideStore source users add once for iOS auto-updates.</summary>
        public string? SidestoreSourceUrl { get; set; }

        public List<MobileAppDownloadFileViewModel> Files { get; set; } = [];
    }

    public record MobileAppDownloadFileViewModel
    {
        public string Name { get; set; } = null!;

        /// <summary>e.g. <c>android-arm64-v8a</c> or <c>ios</c>.</summary>
        public string Platform { get; set; } = null!;

        public long Size { get; set; }

        public string? GithubUrl { get; set; }

        public string? CdnUrl { get; set; }
    }

    /// <summary>
    /// The latest published thin-client packages, as CI recorded them.
    /// </summary>
    /// <remarks>
    /// The client is built for Windows and macOS only; a reader on Linux should be told
    /// that rather than left wondering whether the page failed to load, so the absence
    /// of a platform here is meaningful and the UI says so.
    /// </remarks>
    public record ClientAppDownloadsViewModel
    {
        public string Version { get; set; } = null!;

        public DateTime? PublishedAt { get; set; }

        public string? ReleaseUrl { get; set; }

        public List<ClientAppDownloadFileViewModel> Files { get; set; } = [];
    }

    public record ClientAppDownloadFileViewModel
    {
        public string Name { get; set; } = null!;

        /// <summary>The runtime identifier: <c>win-x64</c>, <c>osx-x64</c> or <c>osx-arm64</c>.</summary>
        public string Platform { get; set; } = null!;

        /// <summary><c>setup</c> for an installer, <c>portable</c> for an archive.</summary>
        public string Shape { get; set; } = null!;

        public long Size { get; set; }

        public string? GithubUrl { get; set; }

        public string? CdnUrl { get; set; }
    }
}
