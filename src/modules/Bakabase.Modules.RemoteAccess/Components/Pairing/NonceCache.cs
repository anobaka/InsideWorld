using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Bakabase.Modules.RemoteAccess.Components.Pairing;

/// <summary>
/// Remembers which nonces a device has already spent, so a signature copied off the
/// wire cannot be replayed inside its five-minute window.
/// </summary>
/// <remarks>
/// <para>
/// Bucketed per device, because one noisy device must not be able to push another
/// device's nonces out and hand an attacker a replay. Each bucket is capped: a device
/// that floods gets its own oldest entries dropped and nobody else's.
/// </para>
/// <para>
/// Entries older than the clock-skew window are already unusable — the timestamp check
/// rejects them before the nonce is consulted — so they are pruned rather than kept.
/// </para>
/// </remarks>
public sealed class NonceCache
{
    private readonly TimeSpan _lifetime;
    private readonly int _maxPerDevice;
    private readonly Func<DateTime> _now;

    private readonly ConcurrentDictionary<string, Bucket> _buckets = new(StringComparer.Ordinal);

    public NonceCache(TimeSpan? lifetime = null, int maxPerDevice = 4096, Func<DateTime>? now = null)
    {
        // Twice the skew window: a request may arrive up to MaxClockSkew early or late,
        // so the span a nonce can be replayed across is the whole width, not half.
        _lifetime = lifetime ?? RemoteRequestSignature.MaxClockSkew * 2;
        _maxPerDevice = maxPerDevice;
        _now = now ?? (() => DateTime.UtcNow);
    }

    private sealed class Bucket
    {
        public readonly Dictionary<string, DateTime> Seen = new(StringComparer.Ordinal);
        public readonly object Gate = new();
    }

    /// <summary>
    /// Records a nonce and reports whether it was new. False means the signature has
    /// already been used and this request is a replay.
    /// </summary>
    public bool TryConsume(string deviceId, string nonce)
    {
        var bucket = _buckets.GetOrAdd(deviceId, _ => new Bucket());
        var now = _now();

        lock (bucket.Gate)
        {
            if (bucket.Seen.TryGetValue(nonce, out var seenAt) && now - seenAt < _lifetime)
            {
                return false;
            }

            if (bucket.Seen.Count >= _maxPerDevice)
            {
                Prune(bucket, now);

                // Still full after pruning: this device is flooding. Drop its oldest
                // rather than refusing it outright — refusing would turn a flood into a
                // denial of service against that one device's own valid requests.
                if (bucket.Seen.Count >= _maxPerDevice)
                {
                    foreach (var stale in bucket.Seen.OrderBy(kv => kv.Value)
                                 .Take(bucket.Seen.Count - _maxPerDevice + 1)
                                 .Select(kv => kv.Key)
                                 .ToArray())
                    {
                        bucket.Seen.Remove(stale);
                    }
                }
            }

            bucket.Seen[nonce] = now;
            return true;
        }
    }

    /// <summary>Drops every device's expired entries. Cheap enough to run on a timer.</summary>
    public void Prune()
    {
        var now = _now();
        foreach (var (deviceId, bucket) in _buckets.ToArray())
        {
            lock (bucket.Gate)
            {
                Prune(bucket, now);
                if (bucket.Seen.Count == 0)
                {
                    _buckets.TryRemove(deviceId, out _);
                }
            }
        }
    }

    public void Forget(string deviceId) => _buckets.TryRemove(deviceId, out _);

    private void Prune(Bucket bucket, DateTime now)
    {
        foreach (var expired in bucket.Seen.Where(kv => now - kv.Value >= _lifetime).Select(kv => kv.Key).ToArray())
        {
            bucket.Seen.Remove(expired);
        }
    }
}
