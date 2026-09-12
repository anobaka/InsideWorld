using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Bakabase.Service.Components.Downloads
{
    /// <summary>
    /// Fetches and caches the download manifests CI publishes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Download URLs only exist once CI has published packages, so CI writes a manifest
    /// to a fixed CDN path and this reads it back. The alternative — composing URLs from
    /// the version — looks simpler until the pipeline renames a file, at which point the
    /// page goes on offering links that 404 and nothing says so.
    /// </para>
    /// <para>
    /// One instance serves every product; the cache is keyed by manifest URL. A failed
    /// fetch is not an error condition: an offline host, a blocked CDN and a product
    /// that has never been published all look the same from here, and all three are
    /// answered with whatever was last known, or null for the page to explain.
    /// </para>
    /// </remarks>
    public class AppDownloadManifestService(ILogger<AppDownloadManifestService> logger)
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

        private static readonly HttpClient Http = new() {Timeout = TimeSpan.FromSeconds(10)};

        private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

        private sealed class Entry
        {
            public readonly SemaphoreSlim Lock = new(1, 1);
            public object? Cached;
            public DateTime FetchedAt = DateTime.MinValue;
        }

        /// <summary>
        /// The manifest at <paramref name="manifestUrl"/>, or null when it cannot be
        /// fetched and nothing is cached.
        /// </summary>
        public async Task<T?> GetAsync<T>(string manifestUrl, CancellationToken ct) where T : class
        {
            var entry = _entries.GetOrAdd(manifestUrl, _ => new Entry());

            if (entry.Cached is T fresh && DateTime.UtcNow - entry.FetchedAt < CacheTtl)
            {
                return fresh;
            }

            await entry.Lock.WaitAsync(ct);
            try
            {
                if (entry.Cached is T stillFresh && DateTime.UtcNow - entry.FetchedAt < CacheTtl)
                {
                    return stillFresh;
                }

                // The CDN caches the manifest object; a fresh query string asks it for the
                // origin's current version without needing a cache purge.
                var url = $"{manifestUrl}?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                var json = await Http.GetStringAsync(url, ct);
                var manifest = JsonConvert.DeserializeObject<T>(json);

                if (manifest != null)
                {
                    entry.Cached = manifest;
                    entry.FetchedAt = DateTime.UtcNow;
                }

                return entry.Cached as T;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning(e, "Could not fetch the download manifest at {Url}", manifestUrl);
                return entry.Cached as T;
            }
            finally
            {
                entry.Lock.Release();
            }
        }
    }
}
