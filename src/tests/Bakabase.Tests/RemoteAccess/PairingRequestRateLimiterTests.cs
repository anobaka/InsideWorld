using System;
using Bakabase.Modules.RemoteAccess.Components.Pairing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Bakabase.Tests.RemoteAccess;

[TestClass]
public class PairingRequestRateLimiterTests
{
    private DateTime _now;
    private PairingRequestRateLimiter _limiter = null!;

    [TestInitialize]
    public void Setup()
    {
        _now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        _limiter = new PairingRequestRateLimiter(() => _now);
    }

    [TestMethod]
    public void An_address_gets_a_few_tries_then_has_to_wait()
    {
        for (var i = 0; i < PairingRequestRateLimiter.MaxPerAddress; i++)
        {
            Assert.IsTrue(_limiter.TryTake("10.0.0.1"), $"attempt {i}");
        }

        Assert.IsFalse(_limiter.TryTake("10.0.0.1"));
    }

    [TestMethod]
    public void One_address_cannot_lock_another_out()
    {
        // A global budget would let anyone on the network deny pairing to everyone else.
        for (var i = 0; i < PairingRequestRateLimiter.MaxPerAddress * 3; i++)
        {
            _limiter.TryTake("10.0.0.1");
        }

        Assert.IsTrue(_limiter.TryTake("10.0.0.2"));
    }

    [TestMethod]
    public void The_budget_refills_as_the_window_passes()
    {
        for (var i = 0; i < PairingRequestRateLimiter.MaxPerAddress; i++)
        {
            _limiter.TryTake("10.0.0.1");
        }

        Assert.IsFalse(_limiter.TryTake("10.0.0.1"));

        _now = _now.Add(PairingRequestRateLimiter.Window);
        Assert.IsTrue(_limiter.TryTake("10.0.0.1"));
    }

    [TestMethod]
    public void An_unknown_address_is_counted_rather_than_exempted()
    {
        // Otherwise stripping the peer address would be the way around the limit.
        for (var i = 0; i < PairingRequestRateLimiter.MaxPerAddress; i++)
        {
            Assert.IsTrue(_limiter.TryTake(null));
        }

        Assert.IsFalse(_limiter.TryTake(null));
        Assert.IsFalse(_limiter.TryTake(""));
    }

    [TestMethod]
    public void Notifications_are_throttled_across_addresses()
    {
        // Each of these stays inside its own request budget, so only the notification
        // throttle stands between them and a wall of notifications.
        Assert.IsTrue(_limiter.TryNotify());
        Assert.IsFalse(_limiter.TryNotify());

        _now = _now.Add(PairingRequestRateLimiter.NotificationInterval);
        Assert.IsTrue(_limiter.TryNotify());
    }
}
