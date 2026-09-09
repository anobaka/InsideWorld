using System;
using Bakabase.Modules.RemoteAccess.Abstractions.Models;

namespace Bakabase.Modules.RemoteAccess.Components.Pairing;

public enum DeviceAuthOutcome
{
    /// <summary>No signature offered. Not an error — most LAN callers are anonymous.</summary>
    Anonymous = 0,

    Authenticated = 1,

    /// <summary>Signed as a device the server no longer knows, i.e. revoked.</summary>
    UnknownDevice = 2,

    /// <summary>The signature does not match. Tampering, or a key that has drifted apart.</summary>
    BadSignature = 3,

    /// <summary>Timestamp outside the skew window. Usually a wrong clock, not an attack.</summary>
    Expired = 4,

    /// <summary>This exact signature has been seen before.</summary>
    Replayed = 5
}

public sealed record DeviceAuthResult(DeviceAuthOutcome Outcome, RemoteDevice? Device)
{
    public static readonly DeviceAuthResult Anonymous = new(DeviceAuthOutcome.Anonymous, null);

    /// <summary>
    /// True for the two outcomes that let a request continue. Everything else is an
    /// explicit failure worth telling the caller about.
    /// </summary>
    public bool MayProceed => Outcome is DeviceAuthOutcome.Anonymous or DeviceAuthOutcome.Authenticated;
}

/// <summary>
/// Decides whether a request carries a valid device signature.
/// </summary>
/// <remarks>
/// Takes primitives rather than an <c>HttpContext</c>, so every rule here — skew,
/// replay, revocation, tampering — is provable without a running server. The web layer
/// is left with nothing but extracting five strings.
/// </remarks>
public sealed class RemoteDeviceAuthenticator(
    IRemoteDeviceService devices,
    NonceCache nonces,
    Func<DateTime>? now = null)
{
    private readonly Func<DateTime> _now = now ?? (() => DateTime.UtcNow);

    /// <param name="bodyDigest">
    /// base64url SHA-256 of the body, or empty when the caller was not expected to hash
    /// it. Both sides derive that expectation from the same observable — a non-GET
    /// request with a Content-Length inside the threshold — so they cannot disagree.
    /// </param>
    public DeviceAuthResult Authenticate(string? authorizationHeader, string method, string path,
        string rawQuery, string bodyDigest)
    {
        var parsed = RemoteRequestSignature.TryParseHeader(authorizationHeader);
        if (parsed == null)
        {
            return DeviceAuthResult.Anonymous;
        }

        var device = devices.Find(parsed.DeviceId);
        if (device == null)
        {
            // Distinct from "no signature": this device believes it is paired, and the
            // UI can say so rather than silently degrading it to anonymous.
            return new DeviceAuthResult(DeviceAuthOutcome.UnknownDevice, null);
        }

        var age = _now() - DateTimeOffset.FromUnixTimeSeconds(parsed.TimestampSeconds).UtcDateTime;
        if (age.Duration() > RemoteRequestSignature.MaxClockSkew)
        {
            // Checked before the signature so a device with a wrong clock is told that,
            // rather than being told its key is bad.
            return new DeviceAuthResult(DeviceAuthOutcome.Expired, device);
        }

        var canonical = RemoteRequestSignature.BuildCanonicalString(
            parsed.DeviceId, method, path, rawQuery, parsed.TimestampSeconds, parsed.Nonce, bodyDigest);

        byte[] key;
        try
        {
            key = RemoteRequestSignature.FromBase64Url(device.Key);
        }
        catch (FormatException)
        {
            // A corrupt stored key cannot authenticate anyone; treat it as a bad
            // signature rather than throwing on the request path.
            return new DeviceAuthResult(DeviceAuthOutcome.BadSignature, device);
        }

        if (!RemoteRequestSignature.Verify(key, canonical, parsed.Signature))
        {
            return new DeviceAuthResult(DeviceAuthOutcome.BadSignature, device);
        }

        // Last, so a replay of a signature that would have failed anyway does not burn a
        // nonce, and so an attacker cannot use nonce exhaustion to probe.
        if (!nonces.TryConsume(parsed.DeviceId, parsed.Nonce))
        {
            return new DeviceAuthResult(DeviceAuthOutcome.Replayed, device);
        }

        return new DeviceAuthResult(DeviceAuthOutcome.Authenticated, device);
    }
}
