using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Bakabase.Modules.RemoteAccess.Components.Pairing;

/// <summary>
/// The wire format a paired device uses to prove who it is, written once and used by
/// both sides: the server verifies with it, the desktop client's forwarding layer
/// signs with it, and the mobile app reimplements it in Dart against the same golden
/// vectors.
/// </summary>
/// <remarks>
/// <para>
/// There is no transport encryption, so the long-term key never travels. Each request
/// carries an HMAC over a canonical description of itself instead, which a passive
/// listener can replay only inside a five-minute window and only once, because the
/// nonce is remembered.
/// </para>
/// <para>
/// The header is <c>Authorization</c> rather than something custom, and that is
/// load-bearing: response caching runs ahead of the MVC filters, and
/// <c>/tool/thumbnail</c> caches on path plus size alone. A request carrying
/// <c>Authorization</c> neither reads nor writes that cache, so a thumbnail a paired
/// device pulled from outside the libraries cannot later be served to an anonymous
/// caller.
/// </para>
/// </remarks>
public static class RemoteRequestSignature
{
    public const string Scheme = "Bakabase-Device";

    /// <summary>Version prefix of the canonical string, so the format can change later.</summary>
    public const string Version = "1";

    /// <summary>
    /// How far a request's timestamp may sit from the server's clock. Clients correct
    /// for the offset using <c>serverTime</c> from the handshake, so this only has to
    /// absorb drift, not a wrong timezone.
    /// </summary>
    public static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Bodies larger than this are signed as if they were empty. Buffering an upload
    /// twice to hash it costs more than the replay protection is worth; the method,
    /// path, query, timestamp and nonce are still covered.
    /// </summary>
    public const int MaxHashedBodyBytes = 1024 * 1024;

    /// <summary>
    /// The exact bytes both sides run the HMAC over. Eight lines, always — the body
    /// digest line is present even for GET, carrying the empty string, so neither
    /// implementation needs a branch that the other might get wrong.
    /// </summary>
    /// <param name="rawQuery">
    /// The query string exactly as it arrived, without the leading '?' and without
    /// re-encoding. Re-encoding is the classic way two implementations disagree:
    /// <c>/file/raw?fullname=</c> carries a whole path in there, and Dio writes a list
    /// as <c>ids=1&amp;ids=2</c>.
    /// </param>
    /// <param name="bodyDigest">
    /// base64url of SHA-256 over the request body, or an empty string when the body
    /// was not hashed (GET/HEAD, or larger than <see cref="MaxHashedBodyBytes"/>).
    /// </param>
    public static string BuildCanonicalString(string deviceId, string method, string path, string rawQuery,
        long timestampSeconds, string nonce, string bodyDigest)
    {
        var sb = new StringBuilder();
        sb.Append(Version).Append('\n');
        sb.Append(deviceId).Append('\n');
        sb.Append(method.ToUpperInvariant()).Append('\n');
        sb.Append(path).Append('\n');
        sb.Append(rawQuery).Append('\n');
        sb.Append(timestampSeconds.ToString(CultureInfo.InvariantCulture)).Append('\n');
        sb.Append(nonce).Append('\n');
        sb.Append(bodyDigest);
        return sb.ToString();
    }

    public static string Sign(byte[] key, string canonicalString) =>
        ToBase64Url(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(canonicalString)));

    /// <summary>
    /// Constant-time comparison, so a wrong signature cannot be narrowed down by
    /// timing how long the rejection took.
    /// </summary>
    public static bool Verify(byte[] key, string canonicalString, string signature)
    {
        if (string.IsNullOrEmpty(signature))
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(Sign(key, canonicalString));
        var actual = Encoding.UTF8.GetBytes(signature);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    public static string HashBody(ReadOnlySpan<byte> body) => ToBase64Url(SHA256.HashData(body));

    /// <summary>
    /// Renders the <c>Authorization</c> header value.
    /// </summary>
    public static string BuildHeader(string deviceId, long timestampSeconds, string nonce, string signature) =>
        $"{Scheme} {deviceId}:{timestampSeconds.ToString(CultureInfo.InvariantCulture)}:{nonce}:{signature}";

    public sealed record Parsed(string DeviceId, long TimestampSeconds, string Nonce, string Signature);

    /// <summary>
    /// Reads a header back. Returns null for anything that is not ours — an absent
    /// header, another scheme, a malformed value. A caller that is simply not paired
    /// is not an error, so this never throws.
    /// </summary>
    public static Parsed? TryParseHeader(string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return null;
        }

        var value = headerValue.Trim();
        if (!value.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var payload = value[Scheme.Length..].Trim();

        // The signature is base64url, so it never contains ':' — splitting into
        // exactly four is safe.
        var parts = payload.Split(':');
        if (parts.Length != 4)
        {
            return null;
        }

        if (parts[0].Length == 0 || parts[2].Length == 0 || parts[3].Length == 0)
        {
            return null;
        }

        if (!long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var timestamp))
        {
            return null;
        }

        return new Parsed(parts[0], timestamp, parts[2], parts[3]);
    }

    /// <summary>
    /// base64url without padding: what fits in a header and a query string without
    /// further escaping.
    /// </summary>
    public static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] FromBase64Url(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }

    /// <summary>A fresh key for a newly paired device. 32 bytes matches HMAC-SHA256's block security.</summary>
    public static byte[] NewDeviceKey() => RandomNumberGenerator.GetBytes(32);

    public static string NewNonce() => ToBase64Url(RandomNumberGenerator.GetBytes(12));
}
