using System;
using System.Collections.Generic;
using Bakabase.Service.Components.RemoteAccess;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Bakabase.Tests.RemoteAccess;

[TestClass]
public class RemoteConnectionRegistryTests
{
    private RemoteConnectionRegistry _registry = null!;
    private List<string> _aborted = null!;

    [TestInitialize]
    public void Setup()
    {
        _registry = new RemoteConnectionRegistry();
        _aborted = [];
    }

    private void Track(string? deviceId, string connectionId) =>
        _registry.Track(deviceId, connectionId, () => _aborted.Add(connectionId));

    [TestMethod]
    public void Revoking_a_device_hangs_up_only_on_that_device()
    {
        Track("device-a", "conn-1");
        Track("device-a", "conn-2");
        Track("device-b", "conn-3");
        Track(null, "conn-4");

        Assert.AreEqual(2, _registry.AbortDevice("device-a"));

        CollectionAssert.AreEquivalent(new[] {"conn-1", "conn-2"}, _aborted);
        Assert.AreEqual(2, _registry.Count);
    }

    [TestMethod]
    public void Requiring_pairing_hangs_up_on_everyone_without_credentials()
    {
        Track(null, "conn-1");
        Track("device-a", "conn-2");
        Track(null, "conn-3");

        Assert.AreEqual(2, _registry.AbortUnpaired());

        CollectionAssert.AreEquivalent(new[] {"conn-1", "conn-3"}, _aborted);
        Assert.AreEqual(1, _registry.Count);
    }

    [TestMethod]
    public void Switching_remote_access_off_hangs_up_on_everyone()
    {
        Track(null, "conn-1");
        Track("device-a", "conn-2");

        Assert.AreEqual(2, _registry.AbortAll());
        Assert.AreEqual(0, _registry.Count);
    }

    [TestMethod]
    public void A_connection_is_never_aborted_twice()
    {
        // Aborting can raise the disconnect callback synchronously, and a stale entry
        // would be swept again by whatever runs next.
        Track("device-a", "conn-1");

        Assert.AreEqual(1, _registry.AbortDevice("device-a"));
        Assert.AreEqual(0, _registry.AbortDevice("device-a"));
        Assert.AreEqual(0, _registry.AbortAll());

        Assert.AreEqual(1, _aborted.Count);
    }

    [TestMethod]
    public void A_connection_that_went_away_on_its_own_does_not_break_the_sweep()
    {
        _registry.Track("device-a", "conn-1", () => throw new ObjectDisposedException("connection"));
        Track("device-a", "conn-2");

        Assert.AreEqual(2, _registry.AbortDevice("device-a"));
        CollectionAssert.AreEquivalent(new[] {"conn-2"}, _aborted);
    }

    [TestMethod]
    public void Forgetting_a_disconnected_connection_leaves_nothing_to_abort()
    {
        Track("device-a", "conn-1");
        _registry.Forget("conn-1");

        Assert.AreEqual(0, _registry.AbortDevice("device-a"));
        Assert.AreEqual(0, _aborted.Count);
    }

    [TestMethod]
    public void Reconnecting_on_the_same_id_replaces_the_old_entry()
    {
        // SignalR reuses a connection id on reconnect. Keeping both would leave a dead
        // abort callback behind that no disconnect will ever clear.
        Track(null, "conn-1");
        Track("device-a", "conn-1");

        Assert.AreEqual(1, _registry.Count);
        Assert.AreEqual(0, _registry.AbortUnpaired());
        Assert.AreEqual(1, _registry.AbortDevice("device-a"));
    }
}
