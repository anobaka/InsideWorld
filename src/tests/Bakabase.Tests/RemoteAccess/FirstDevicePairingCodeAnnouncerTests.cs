using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Bakabase.Abstractions.Models.Domain.Constants;
using Bakabase.Abstractions.Models.Domain.Options;
using Bakabase.Modules.RemoteAccess.Abstractions.Components;
using Bakabase.Modules.RemoteAccess.Abstractions.Models;
using Bakabase.Modules.RemoteAccess.Components.Pairing;
using Bakabase.Modules.RemoteAccess.Services;
using Bakabase.Service.Components.RemoteAccess;
using Bakabase.TestKit.Implementations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Bakabase.Tests.RemoteAccess;

/// <summary>
/// Locks down when a pairing code is printed to the log, which on a server with no
/// screen is the only way in.
/// </summary>
[TestClass]
public class FirstDevicePairingCodeAnnouncerTests
{
    private string _root = null!;
    private RemoteAccessService _access = null!;
    private RemoteDeviceService _devices = null!;
    private FirstDevicePairingCodeAnnouncer _announcer = null!;
    private RemoteAccessDataDirectory _directory = null!;

    /// <summary>
    /// Reports whether anything was ever created, so a test can prove the announcer
    /// leaves the disk alone while it has nothing to say.
    /// </summary>
    private sealed class RemoteAccessDataDirectory(string path) : IRemoteAccessDataDirectory
    {
        public string Path => path;

        public string Ensure()
        {
            Ensured = true;
            return Directory.CreateDirectory(path).FullName;
        }

        public bool Ensured { get; private set; }
    }

    private sealed class StubListeningAddressProvider : IListeningAddressProvider
    {
        public IReadOnlyList<string> GetListeningAddresses() => [];
    }

    [TestInitialize]
    public void Setup()
    {
        _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bakabase-announcer-tests",
            Guid.NewGuid().ToString("N"));

        _access = new RemoteAccessService(
            new TestBOptionsManager<RemoteAccessOptions>(new RemoteAccessOptions()),
            new RemoteAccessDefaults(RemoteAccessMode.Disabled),
            new RemoteAccessHostInfo("1.2.3-test"),
            new StubListeningAddressProvider(),
            NullLogger<RemoteAccessService>.Instance);

        _directory = new RemoteAccessDataDirectory(System.IO.Path.Combine(_root, "remote-access"));
        _devices = new RemoteDeviceService(new RemoteDeviceStore(_directory));
        _announcer = new FirstDevicePairingCodeAnnouncer(_access, _devices,
            NullLogger<FirstDevicePairingCodeAnnouncer>.Instance);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            Directory.Delete(_root, true);
        }
        catch (Exception e) when (e is IOException or DirectoryNotFoundException)
        {
        }
    }

    private async Task LockOut()
    {
        await _access.SetModeAsync(RemoteAccessMode.Enabled);
        await _access.SetRequirePairingAsync(true);
    }

    [TestMethod]
    public async Task A_locked_out_server_prints_a_code()
    {
        await LockOut();

        Assert.IsTrue(await _announcer.AnnounceIfLockedOutAsync());
        Assert.IsNotNull(_devices.GetPairingCodeStatus());
    }

    [TestMethod]
    public async Task Nothing_is_printed_while_a_code_is_still_good()
    {
        // Re-issuing would invalidate the code somebody is in the middle of typing.
        await LockOut();
        Assert.IsTrue(await _announcer.AnnounceIfLockedOutAsync());

        var issued = _devices.GetPairingCodeStatus()!.ExpiresAt;
        Assert.IsFalse(await _announcer.AnnounceIfLockedOutAsync());
        Assert.AreEqual(issued, _devices.GetPairingCodeStatus()!.ExpiresAt);
    }

    [TestMethod]
    public async Task Nothing_is_printed_once_a_device_has_paired()
    {
        await LockOut();
        Assert.IsTrue(await _announcer.AnnounceIfLockedOutAsync());

        var issue = await _devices.IssuePairingCodeAsync();
        Assert.IsTrue((await _devices.PairWithCodeAsync(issue.Code, "Phone", RemoteDevicePlatform.Android))
            .Succeeded);

        // The code that let the first device in is spent, so the only thing keeping the
        // announcer quiet now is that somebody is paired.
        Assert.IsNull(_devices.GetPairingCodeStatus());
        Assert.IsFalse(await _announcer.AnnounceIfLockedOutAsync());
    }

    [TestMethod]
    public async Task Nothing_is_printed_when_pairing_is_not_required()
    {
        // The common container setup: reachable by anyone who can reach the port, which
        // is what it has always been. A code nobody needs would be noise.
        await _access.SetModeAsync(RemoteAccessMode.Enabled);

        Assert.IsFalse(await _announcer.AnnounceIfLockedOutAsync());
    }

    [TestMethod]
    public async Task Nothing_is_printed_in_unrestricted_mode()
    {
        // Unrestricted ignores pairing altogether, so there is nothing to be locked out
        // of even with the switch on.
        await _access.SetModeAsync(RemoteAccessMode.Unrestricted);
        await _access.SetRequirePairingAsync(true);

        Assert.IsFalse(await _announcer.AnnounceIfLockedOutAsync());
    }

    [TestMethod]
    public async Task Nothing_is_printed_while_remote_access_is_off()
    {
        // The desktop default. A code would not help anyone, and this is the case that
        // must not touch the disk.
        await _access.SetRequirePairingAsync(true);

        Assert.IsFalse(await _announcer.AnnounceIfLockedOutAsync());
        Assert.IsFalse(_directory.Ensured);
        Assert.IsFalse(Directory.Exists(_directory.Path));
    }
}
