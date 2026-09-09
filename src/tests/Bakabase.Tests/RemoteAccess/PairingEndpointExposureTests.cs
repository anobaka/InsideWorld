using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bakabase.Service.Components.RemoteAccess;
using Bakabase.Service.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Bakabase.Tests.RemoteAccess;

/// <summary>
/// Holds the pairing routes and the middleware's anonymous allowlist against each other.
/// </summary>
/// <remarks>
/// The two have to agree, and neither direction of disagreement is visible by reading
/// one file: a management route that slips under the allowlist lets an unpaired caller
/// approve itself, and a pairing route that falls outside it locks out the very device
/// that has no credentials yet. This is not hypothetical — writing the controller
/// against a <c>/remote-access/pair</c> prefix (no trailing slash) produced the first of
/// those, because it also matched <c>/remote-access/pairing/</c>.
/// </remarks>
[TestClass]
public class PairingEndpointExposureTests
{
    /// <summary>Everything a device with no key may call, by prefix.</summary>
    private const string AnonymousPrefix = "/remote-access/pair/";

    /// <summary>Everything only the host or an already-paired device may call.</summary>
    private static readonly string[] ManagementPrefixes =
    [
        "/remote-access/pairing/",
        "/remote-access/devices"
    ];

    private sealed record Route(string Method, string Path, bool RemoteAccessible)
    {
        public override string ToString() => $"{Method} {Path}";
    }

    private static IReadOnlyList<Route> Routes()
    {
        var prefix = typeof(RemoteAccessController).GetCustomAttribute<RouteAttribute>()!.Template
            .TrimStart('~').Trim('/');

        return typeof(RemoteAccessController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(action => action.GetCustomAttributes<HttpMethodAttribute>()
                .SelectMany(http => http.HttpMethods.Select(method =>
                {
                    var template = http.Template?.Trim('/') ?? string.Empty;
                    var path = template.Length == 0 ? $"/{prefix}" : $"/{prefix}/{template}";
                    return new Route(method, path,
                        action.GetCustomAttribute<RemoteAccessibleAttribute>()?.Allowed ?? false);
                })))
            .ToArray();
    }

    private static bool IsAllowlisted(string path) =>
        RemoteAccessMiddleware.AnonymousPathPrefixes.Any(p =>
            path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    [TestMethod]
    public void Every_pairing_route_is_reachable_without_credentials()
    {
        // Both gates have to let these through: the middleware's RequirePairing check
        // and the per-action filter. Missing either one means a fresh device cannot
        // pair, which is the one thing this group of routes exists for.
        var routes = Routes().Where(r => r.Path.StartsWith(AnonymousPrefix, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.IsTrue(routes.Length >= 3, $"expected the pair/ routes to exist, found {routes.Length}");

        var unmarked = routes.Where(r => !r.RemoteAccessible).Select(r => r.ToString()).ToArray();
        Assert.AreEqual(0, unmarked.Length, $"not [RemoteAccessible]: {string.Join(", ", unmarked)}");

        var unlisted = routes.Where(r => !IsAllowlisted(r.Path)).Select(r => r.ToString()).ToArray();
        Assert.AreEqual(0, unlisted.Length, $"outside the anonymous allowlist: {string.Join(", ", unlisted)}");
    }

    [TestMethod]
    public void No_management_route_is_reachable_without_credentials()
    {
        var routes = Routes()
            .Where(r => ManagementPrefixes.Any(p => r.Path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        Assert.IsTrue(routes.Length >= 5, $"expected the management routes to exist, found {routes.Length}");

        // Marking one of these would let any LAN caller approve its own pairing request.
        var marked = routes.Where(r => r.RemoteAccessible).Select(r => r.ToString()).ToArray();
        Assert.AreEqual(0, marked.Length, $"must not be [RemoteAccessible]: {string.Join(", ", marked)}");

        var listed = routes.Where(r => IsAllowlisted(r.Path)).Select(r => r.ToString()).ToArray();
        Assert.AreEqual(0, listed.Length, $"must not be in the anonymous allowlist: {string.Join(", ", listed)}");
    }

    [TestMethod]
    public void Nothing_carries_a_secret_in_its_url()
    {
        // A pairing code or a request id in a path or query string ends up in access
        // logs, proxy caches and browser history. They travel in bodies instead, which
        // is why all three pair/ routes are POSTs.
        var offenders = Routes()
            .Where(r => r.Path.StartsWith(AnonymousPrefix, StringComparison.OrdinalIgnoreCase))
            .Where(r => r.Path.Contains('{') || !string.Equals(r.Method, "POST", StringComparison.Ordinal))
            .Select(r => r.ToString())
            .ToArray();

        Assert.AreEqual(0, offenders.Length, $"secrets belong in the body: {string.Join(", ", offenders)}");
    }

    [TestMethod]
    public void The_allowlist_has_no_prefix_that_swallows_a_management_route()
    {
        // Guards the allowlist itself, independent of what the controller currently
        // declares: a prefix like "/remote-access/pair" (no slash) would pass the tests
        // above today and start swallowing management routes the moment one is added.
        var swallowed = RemoteAccessMiddleware.AnonymousPathPrefixes
            .SelectMany(prefix => ManagementPrefixes
                .Where(m => m.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(m => $"{prefix} covers {m}"))
            .ToArray();

        Assert.AreEqual(0, swallowed.Length, string.Join(", ", swallowed));
    }
}
