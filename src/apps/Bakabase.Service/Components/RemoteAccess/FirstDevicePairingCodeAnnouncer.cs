using System;
using System.Threading;
using System.Threading.Tasks;
using Bakabase.Abstractions.Models.Domain.Constants;
using Bakabase.Modules.RemoteAccess.Abstractions.Services;
using Bakabase.Modules.RemoteAccess.Components.Pairing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bakabase.Service.Components.RemoteAccess;

/// <summary>
/// Prints a pairing code whenever the server is locked out of itself: pairing is
/// required, and no device has paired yet.
/// </summary>
/// <remarks>
/// <para>
/// In that state every remote caller is refused and there is nobody to approve a
/// request, so the only way in is a code — and on a server with no screen, the log is
/// the only place to put one. The container build is exactly that server.
/// </para>
/// <para>
/// The condition is the lockout itself rather than the runtime flavour. Gating on
/// "is this the container build" would read the same today and quietly exclude the
/// headless server build when it arrives; gating on the lockout keeps working, and
/// says out loud what the code is for.
/// </para>
/// <para>
/// Nothing is written, and no directory is created, unless the lockout actually holds:
/// the mode and the pairing switch are read first, and both come from options already
/// in memory.
/// </para>
/// </remarks>
public sealed class FirstDevicePairingCodeAnnouncer(
    IRemoteAccessService remoteAccessService,
    IRemoteDeviceService deviceService,
    ILogger<FirstDevicePairingCodeAnnouncer> logger) : BackgroundService
{
    /// <summary>
    /// How often the lockout is re-checked. Every input is already in memory, so this
    /// costs nothing; it is short enough that a code which lapses is replaced before
    /// anyone reading the log has time to type the old one.
    /// </summary>
    public static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);

        try
        {
            do
            {
                try
                {
                    await AnnounceIfLockedOutAsync(stoppingToken);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    // Never take the host down over this: the server still works for
                    // loopback, and a failure here only means nobody is told the code.
                    logger.LogError(e, "Failed to announce a pairing code");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }

    /// <summary>
    /// One pass of the check. Public so the rule about when a code is printed — which
    /// is the whole of this class — can be exercised without a running host.
    /// </summary>
    /// <returns>Whether a code was issued and printed.</returns>
    public async Task<bool> AnnounceIfLockedOutAsync(CancellationToken ct = default)
    {
        if (!IsLockedOut())
        {
            return false;
        }

        // A code the operator issued from the settings page counts; re-issuing would
        // invalidate the one they are in the middle of typing.
        if (deviceService.GetPairingCodeStatus() != null)
        {
            return false;
        }

        var issue = await deviceService.IssuePairingCodeAsync(ct: ct);
        Announce(issue.Code, issue.ExpiresAt);
        return true;
    }

    private bool IsLockedOut()
    {
        var mode = remoteAccessService.GetEffectiveMode();

        // Unrestricted ignores pairing altogether, so there is nothing to be locked out
        // of; Disabled serves nobody remote, and a code would not help.
        if (mode != RemoteAccessMode.Enabled || !remoteAccessService.GetRequirePairing())
        {
            return false;
        }

        return !deviceService.HasAnyDevice;
    }

    private void Announce(string code, DateTime expiresAt)
    {
        var minutes = Math.Max(1, (int) Math.Round((expiresAt - DateTime.UtcNow).TotalMinutes));
        var message =
            $"No device has paired with Bakabase yet, and pairing is required. " +
            $"Enter this code on the first device: {code} (valid for {minutes} minutes)";

        logger.LogWarning("{Message}", message);

        // Written to stdout as well as to the log, because a container's logging
        // configuration is not ours to assume and this is the one message an operator
        // cannot get any other way.
        Console.Out.WriteLine();
        Console.Out.WriteLine("  ================ Bakabase pairing code ================");
        Console.Out.WriteLine($"    {code}");
        Console.Out.WriteLine($"    valid for {minutes} minutes");
        Console.Out.WriteLine("  =======================================================");
        Console.Out.WriteLine();
        Console.Out.Flush();
    }
}
