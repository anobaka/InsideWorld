using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Bakabase.Modules.RemoteAccess.Components.Pairing;

/// <summary>
/// A URL a paired device can hand to something that cannot sign anything: an external
/// player, a subtitle downloader, an <c>&lt;img&gt;</c> tag.
/// </summary>
/// <remarks>
/// <para>
/// The device signs these itself, with the same key it signs headers with. There is no
/// issuing endpoint and no server-held secret, which is what makes revocation total: the
/// server verifies against the device's stored key, so removing the device kills every
/// link it ever produced, including ones already sitting in a player's playlist.
/// </para>
/// <para>
/// The token covers one URL — method, path and the rest of the query — so it cannot be
/// carried to another endpoint or another file. It deliberately has no nonce: video
/// playback issues dozens of range requests against the same URL, and treating the
/// second one as a replay would break playback rather than protect anything.
/// </para>
/// <para>
/// A token does travel in the URL, and therefore into access logs and browser history.
/// That is the cost of the one thing it exists for, bounded by covering a single URL
/// and by <see cref="MaxLifetime"/>.
/// </para>
/// </remarks>
public static class SignedMediaUrl
{
    /// <summary>Query parameter the token rides in.</summary>
    public const string QueryKey = "bkbt";

    /// <summary>
    /// First line of the canonical string, and the token's own prefix. Deliberately
    /// unlike <see cref="RemoteRequestSignature.Version"/>: the two formats are signed
    /// with the same key, and differing here is what stops a captured header signature
    /// from being replayed as a URL token, or the reverse.
    /// </summary>
    public const string Version = "bkb-url-1";

    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(12);

    /// <summary>
    /// The furthest expiry the server will honour. The client picks the lifetime, so
    /// without a cap a device could mint a link that outlives any reason to trust it.
    /// </summary>
    public static readonly TimeSpan MaxLifetime = TimeSpan.FromDays(7);

    /// <summary>
    /// The bytes both sides run the HMAC over. The method is part of it and is always
    /// <c>GET</c> in practice — these are links to fetch, and pinning it stops a token
    /// from being turned into a write.
    /// </summary>
    /// <param name="rawQuery">
    /// The query exactly as it will appear on the wire, minus the token's own parameter
    /// and without the leading '?'. Not re-encoded and not reordered: the client builds
    /// the URL it is about to hand out, so what it signs is what it sends.
    /// </param>
    public static string BuildCanonicalString(string deviceId, string method, string path, string rawQuery,
        long expiresAtSeconds)
    {
        var sb = new StringBuilder();
        sb.Append(Version).Append('\n');
        sb.Append(deviceId).Append('\n');
        sb.Append(method.ToUpperInvariant()).Append('\n');
        sb.Append(path).Append('\n');
        sb.Append(rawQuery).Append('\n');
        sb.Append(expiresAtSeconds.ToString(CultureInfo.InvariantCulture));
        return sb.ToString();
    }

    /// <summary>
    /// Builds the token for one URL. <paramref name="rawQuery"/> must already be the
    /// final query minus this parameter; append <c>{QueryKey}={token}</c> to it.
    /// </summary>
    public static string BuildToken(byte[] key, string deviceId, string path, string rawQuery, DateTime expiresAt)
    {
        var expiry = new DateTimeOffset(expiresAt.ToUniversalTime()).ToUnixTimeSeconds();
        var canonical = BuildCanonicalString(deviceId, "GET", path, rawQuery, expiry);
        var signature = RemoteRequestSignature.Sign(key, canonical);

        return $"{Version}.{deviceId}.{expiry.ToString(CultureInfo.InvariantCulture)}.{signature}";
    }

    /// <summary>
    /// Renders a complete relative URL with the token appended, which is what a client
    /// hands to a player.
    /// </summary>
    public static string BuildUrl(byte[] key, string deviceId, string path, string rawQuery, DateTime expiresAt)
    {
        var token = BuildToken(key, deviceId, path, rawQuery, expiresAt);
        var query = rawQuery.Length == 0 ? $"{QueryKey}={token}" : $"{rawQuery}&{QueryKey}={token}";
        return $"{path}?{query}";
    }

    public sealed record Parsed(string DeviceId, long ExpiresAtSeconds, string Signature);

    /// <summary>
    /// Reads a token back, or null for anything that is not one. A URL that simply
    /// carries no token is not an error, so this never throws.
    /// </summary>
    public static Parsed? TryParse(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        // Version, device id and signature are all dot-free (base64url and a literal),
        // so splitting into exactly four is unambiguous.
        var parts = token.Split('.');
        if (parts.Length != 4 || !string.Equals(parts[0], Version, StringComparison.Ordinal))
        {
            return null;
        }

        if (parts[1].Length == 0 || parts[3].Length == 0)
        {
            return null;
        }

        if (!long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var expiry))
        {
            return null;
        }

        return new Parsed(parts[1], expiry, parts[3]);
    }

    /// <summary>
    /// The query as it was signed: the same string with this parameter removed. A
    /// textual operation on <c>key=value</c> pairs, so nothing is re-encoded and the
    /// order the client chose survives.
    /// </summary>
    public static string StripToken(string rawQuery)
    {
        if (rawQuery.Length == 0 || !rawQuery.Contains(QueryKey, StringComparison.OrdinalIgnoreCase))
        {
            return rawQuery;
        }

        return string.Join('&', rawQuery.Split('&').Where(pair =>
        {
            var separator = pair.IndexOf('=');
            var key = separator < 0 ? pair : pair[..separator];
            return !string.Equals(key, QueryKey, StringComparison.OrdinalIgnoreCase);
        }));
    }
}
