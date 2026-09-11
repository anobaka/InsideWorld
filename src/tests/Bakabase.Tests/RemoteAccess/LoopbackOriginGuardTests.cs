using System;
using System.Collections.Generic;
using Bakabase.Client.Remoting.Components.Forwarding;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Bakabase.Tests.RemoteAccess;

/// <summary>
/// The only thing standing between a local port and every page the user has open.
/// </summary>
/// <remarks>
/// The forwarding layer has no authentication of its own — reaching it is reaching the
/// user's whole library, signed with the device key — so this is where a mistake is
/// expensive, and where the tests have to be specific about what an attack looks like.
/// </remarks>
[TestClass]
public class LoopbackOriginGuardTests
{
    private const int Port = 34568;
    private static readonly LoopbackOriginGuard Guard = new(Port);

    private static LoopbackGuardVerdict Get(string? host, string? origin = null) =>
        Guard.Evaluate(host, origin, "GET");

    private static LoopbackGuardVerdict Post(string? host, string? origin = null) =>
        Guard.Evaluate(host, origin, "POST");

    [TestMethod]
    public void The_clients_own_window_is_served()
    {
        Assert.AreEqual(LoopbackGuardVerdict.Allowed, Get($"127.0.0.1:{Port}"));
        Assert.AreEqual(LoopbackGuardVerdict.Allowed, Get($"localhost:{Port}"));
        Assert.AreEqual(LoopbackGuardVerdict.Allowed,
            Post($"127.0.0.1:{Port}", $"http://127.0.0.1:{Port}"));
        Assert.AreEqual(LoopbackGuardVerdict.Allowed,
            Post($"localhost:{Port}", $"http://localhost:{Port}"));
    }

    [TestMethod]
    public void A_rebound_hostname_is_refused()
    {
        // DNS rebinding: evil.com resolves to 127.0.0.1, so the connection genuinely
        // arrives here — but the browser sends the hostname from the URL bar, and that
        // is still evil.com. This check is the whole defence.
        foreach (var host in new[] {$"evil.com:{Port}", $"bakabase.evil.com:{Port}", $"192.168.1.5:{Port}"})
        {
            Assert.AreEqual(LoopbackGuardVerdict.ForeignHost, Get(host), host);
        }
    }

    [TestMethod]
    public void Another_port_on_this_machine_is_refused()
    {
        // A different local listener's origin is not ours, and a page served by one has
        // no business reaching this.
        Assert.AreEqual(LoopbackGuardVerdict.ForeignHost, Get($"127.0.0.1:{Port + 1}"));
        Assert.AreEqual(LoopbackGuardVerdict.ForeignHost, Get("127.0.0.1"));
    }

    [TestMethod]
    public void A_missing_host_is_refused()
    {
        // HTTP/1.1 requires the header and HTTP/2 requires :authority; something that
        // sends neither is not the embedded browser.
        Assert.AreEqual(LoopbackGuardVerdict.ForeignHost, Get(null));
        Assert.AreEqual(LoopbackGuardVerdict.ForeignHost, Get(""));
    }

    [TestMethod]
    public void A_hostname_that_merely_contains_a_loopback_name_is_refused()
    {
        // The kind of near-miss a prefix or substring check would let through.
        foreach (var host in new[]
                 {
                     $"127.0.0.1.evil.com:{Port}", $"localhost.evil.com:{Port}",
                     $"notlocalhost:{Port}", $"127.0.0.10:{Port}"
                 })
        {
            Assert.AreEqual(LoopbackGuardVerdict.ForeignHost, Get(host), host);
        }
    }

    [TestMethod]
    public void A_cross_site_write_is_refused_even_with_an_honest_host()
    {
        // A page on evil.com can POST to http://127.0.0.1:34568 directly — no rebinding
        // needed, and the Host is truthful. The browser will not hand it the response,
        // but a delete that already happened did not need one.
        Assert.AreEqual(LoopbackGuardVerdict.ForeignOrigin, Post($"127.0.0.1:{Port}", "https://evil.com"));
        Assert.AreEqual(LoopbackGuardVerdict.ForeignOrigin,
            Post($"127.0.0.1:{Port}", $"http://127.0.0.1:{Port + 1}"));
        Assert.AreEqual(LoopbackGuardVerdict.ForeignOrigin, Post($"127.0.0.1:{Port}", "null"));
    }

    [TestMethod]
    public void Every_state_changing_method_is_covered()
    {
        foreach (var method in new[] {"POST", "PUT", "PATCH", "DELETE"})
        {
            Assert.AreEqual(LoopbackGuardVerdict.ForeignOrigin,
                Guard.Evaluate($"127.0.0.1:{Port}", "https://evil.com", method), method);
        }
    }

    [TestMethod]
    public void A_cross_site_read_is_already_stopped_by_the_host_check()
    {
        // GET carries no Origin from an <img> or a <script>, so there is nothing to
        // check there — which is fine, because such a request can only arrive with a
        // foreign Host, and the browser will not let the page read what comes back.
        Assert.AreEqual(LoopbackGuardVerdict.Allowed, Get($"127.0.0.1:{Port}", "https://evil.com"));
        Assert.AreEqual(LoopbackGuardVerdict.ForeignHost, Get($"evil.com:{Port}", "https://evil.com"));
    }

    [TestMethod]
    public void A_write_with_no_origin_at_all_is_allowed()
    {
        // Browsers always send Origin on a state-changing request. What omits it is a
        // local player fetching a stream, or a script the user ran themselves — neither
        // is the vector this guards, and refusing them would break playback.
        Assert.AreEqual(LoopbackGuardVerdict.Allowed, Post($"127.0.0.1:{Port}"));
        Assert.AreEqual(LoopbackGuardVerdict.Allowed, Post($"127.0.0.1:{Port}", ""));
    }

    [TestMethod]
    public void Case_does_not_decide_anything()
    {
        Assert.AreEqual(LoopbackGuardVerdict.Allowed, Get($"LOCALHOST:{Port}"));
        Assert.AreEqual(LoopbackGuardVerdict.Allowed, Post($"127.0.0.1:{Port}", $"HTTP://LOCALHOST:{Port}"));
    }

    // ---- port allocation ----

    [TestMethod]
    public void The_same_port_is_chosen_every_launch_when_it_is_free()
    {
        // Browser storage is keyed to the origin, port included. A client that moved
        // ports between launches would look to the user like it had forgotten their
        // settings.
        Assert.AreEqual(LoopbackPortAllocator.PreferredPort,
            LoopbackPortAllocator.Allocate(isFree: _ => true));
    }

    [TestMethod]
    public void A_taken_port_is_stepped_over_rather_than_randomised()
    {
        var taken = new HashSet<int>
        {
            LoopbackPortAllocator.PreferredPort,
            LoopbackPortAllocator.PreferredPort + 1
        };

        Assert.AreEqual(LoopbackPortAllocator.PreferredPort + 2,
            LoopbackPortAllocator.Allocate(isFree: p => !taken.Contains(p)));
    }

    [TestMethod]
    public void A_machine_with_nothing_free_fails_loudly()
    {
        // Silently binding somewhere unexpected would leave the embedded browser
        // pointed at a port nothing is listening on.
        Assert.ThrowsException<System.IO.IOException>(() => LoopbackPortAllocator.Allocate(isFree: _ => false));
    }
}
