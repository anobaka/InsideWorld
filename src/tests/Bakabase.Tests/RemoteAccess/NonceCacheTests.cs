using System;
using System.Linq;
using Bakabase.Modules.RemoteAccess.Components.Pairing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Bakabase.Tests.RemoteAccess;

[TestClass]
public class NonceCacheTests
{
    private DateTime _now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private NonceCache Build(int maxPerDevice = 4096) =>
        new(TimeSpan.FromMinutes(10), maxPerDevice, () => _now);

    [TestMethod]
    public void A_nonce_is_accepted_once_and_then_refused()
    {
        var cache = Build();

        Assert.IsTrue(cache.TryConsume("dev-1", "n1"));
        Assert.IsFalse(cache.TryConsume("dev-1", "n1"));
    }

    [TestMethod]
    public void Devices_do_not_share_a_namespace()
    {
        // Two devices picking the same random nonce is unremarkable, and one must not
        // invalidate the other's request.
        var cache = Build();

        Assert.IsTrue(cache.TryConsume("dev-1", "n1"));
        Assert.IsTrue(cache.TryConsume("dev-2", "n1"));
    }

    [TestMethod]
    public void A_nonce_becomes_reusable_once_it_is_older_than_the_window()
    {
        // By then the timestamp check has already rejected the request, so remembering
        // it any longer only costs memory.
        var cache = Build();
        Assert.IsTrue(cache.TryConsume("dev-1", "n1"));

        _now = _now.AddMinutes(10);

        Assert.IsTrue(cache.TryConsume("dev-1", "n1"));
    }

    [TestMethod]
    public void A_flooding_device_evicts_only_its_own_entries()
    {
        var cache = Build(maxPerDevice: 8);

        Assert.IsTrue(cache.TryConsume("quiet", "keep-me"));

        foreach (var i in Enumerable.Range(0, 50))
        {
            Assert.IsTrue(cache.TryConsume("noisy", $"n{i}"));
        }

        // The quiet device's single nonce survived the flood, so a replay against it is
        // still caught.
        Assert.IsFalse(cache.TryConsume("quiet", "keep-me"));
    }

    [TestMethod]
    public void A_flooding_device_keeps_being_served()
    {
        // Refusing it outright would turn a flood into a denial of service against that
        // device's own valid requests.
        var cache = Build(maxPerDevice: 4);

        foreach (var i in Enumerable.Range(0, 100))
        {
            Assert.IsTrue(cache.TryConsume("noisy", $"n{i}"), $"rejected fresh nonce n{i}");
        }
    }

    [TestMethod]
    public void Pruning_drops_expired_entries()
    {
        var cache = Build();
        cache.TryConsume("dev-1", "n1");

        _now = _now.AddMinutes(11);
        cache.Prune();
        _now = _now.AddMinutes(-11);

        Assert.IsTrue(cache.TryConsume("dev-1", "n1"));
    }

    [TestMethod]
    public void Forgetting_a_device_clears_its_history()
    {
        // Revoking a device should not leave its nonces pinning memory.
        var cache = Build();
        cache.TryConsume("dev-1", "n1");

        cache.Forget("dev-1");

        Assert.IsTrue(cache.TryConsume("dev-1", "n1"));
    }
}
