using System;

namespace Bakabase.Modules.RemoteAccess.Abstractions.Models;

/// <summary>
/// A freshly issued pairing code. The plaintext is returned exactly once, here — only
/// its digest is persisted, so nothing that reads <c>devices.json</c> later can hand
/// out a working code.
/// </summary>
public sealed record PairingCodeIssue(string Code, DateTime ExpiresAt);

/// <summary>
/// What the settings page may show about the outstanding code: that there is one and
/// when it lapses, never the code itself once it has been displayed.
/// </summary>
public sealed record PairingCodeStatus(DateTime ExpiresAt, int RemainingAttempts);

/// <summary>
/// Why a pairing attempt did not produce credentials. Kept coarse on purpose: telling
/// a caller "the code was right but expired" narrows a guess for them.
/// </summary>
public enum PairingFailure
{
    None = 0,

    /// <summary>No code outstanding, wrong code, expired, or burned by failed attempts.</summary>
    CodeRejected = 1,

    /// <summary>The request id is unknown, already claimed, or has expired.</summary>
    RequestRejected = 2,

    /// <summary>Approved, but the requesting device has not collected its key yet.</summary>
    NotYetApproved = 3
}

/// <summary>
/// Credentials handed to a device that has just paired. The key appears here once and
/// is never returned again.
/// </summary>
public sealed record PairingCredentials(string DeviceId, string Key, string ServerId);

public sealed record PairingResult(PairingCredentials? Credentials, PairingFailure Failure)
{
    public static PairingResult Ok(PairingCredentials credentials) => new(credentials, PairingFailure.None);
    public static PairingResult Failed(PairingFailure failure) => new(null, failure);
    public bool Succeeded => Credentials != null;
}
