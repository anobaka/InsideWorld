using System;
using System.Collections.Generic;

namespace Bakabase.Modules.RemoteAccess.Abstractions.Models;

/// <summary>
/// The OS family of a paired device. Drives which native-player hand-off links the UI
/// offers for it.
/// </summary>
public enum RemoteDevicePlatform
{
    Unknown = 0,
    Windows = 1,
    MacOS = 2,
    Linux = 3,
    Android = 4,
    IOS = 5
}

/// <summary>
/// A device that has been paired for remote (non-loopback) access.
/// </summary>
/// <remarks>
/// The key is stored in the clear, which is forced by the choice of HMAC: verifying a
/// symmetric signature needs the same bytes the client signed with, so a digest cannot
/// stand in. That is precisely why this file may never become an <c>[Options]</c> type
/// — every options object is pushed wholesale to any client that opens the UI hub.
/// </remarks>
public class RemoteDevice
{
    public string Id { get; set; } = null!;

    public string Name { get; set; } = null!;

    public RemoteDevicePlatform Platform { get; set; }

    /// <summary>base64url of the 32-byte HMAC key shared with this device.</summary>
    public string Key { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Last time this device made a signed request. Written at most once every
    /// <see cref="LastSeenPersistenceInterval"/> so ordinary browsing does not rewrite
    /// the file on every request.
    /// </summary>
    public DateTime? LastSeenAt { get; set; }

    /// <summary>
    /// Which device approved this one, or null for the first device, which used a
    /// pairing code instead. Kept so a device list can answer "who let this in".
    /// </summary>
    public string? ApprovedByDeviceId { get; set; }

    public static readonly TimeSpan LastSeenPersistenceInterval = TimeSpan.FromMinutes(10);
}

/// <summary>
/// A device asking to be let in, waiting for an already-paired device to approve it.
/// </summary>
public class PendingPairingRequest
{
    public string Id { get; set; } = null!;
    public string DeviceName { get; set; } = null!;
    public RemoteDevicePlatform Platform { get; set; }

    /// <summary>Where the request came from, so the approver can sanity-check it.</summary>
    public string? RemoteAddress { get; set; }

    public DateTime RequestedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// The outstanding pairing code. Only its digest is kept, so reading the file does not
/// hand over a working code.
/// </summary>
public class PairingCodeState
{
    /// <summary>base64url of SHA-256 over the code.</summary>
    public string CodeHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    /// <summary>Wrong guesses so far. The code is burned after enough of them.</summary>
    public int FailedAttempts { get; set; }

    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(10);
    public const int CodeDigits = 6;
}

/// <summary>
/// Everything <c>devices.json</c> holds. Deliberately not an <c>[Options]</c> type: it
/// carries device keys and the signing secret, and options are broadcast to every UI
/// hub client.
/// </summary>
public class RemoteDeviceStoreData
{
    public List<RemoteDevice> Devices { get; set; } = [];

    public List<PendingPairingRequest> PendingRequests { get; set; } = [];

    public PairingCodeState? PairingCode { get; set; }

    /// <summary>
    /// base64url secret used to sign media URLs, which are handed to players that
    /// cannot carry a header. Rotating it invalidates every outstanding URL.
    /// </summary>
    public string? SigningSecret { get; set; }
}
