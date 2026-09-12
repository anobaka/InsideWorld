using System;
using Bakabase.Client.Remoting.Components.Updating;
using Bakabase.Service.Components;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Bakabase.Tests.RemoteAccess;

/// <summary>
/// Where each flavour looks for its own updates.
/// </summary>
/// <remarks>
/// Velopack's updater installs whatever the feed at the configured base offers, so these
/// two strings are what keep one product from replacing the other. The release pipeline
/// publishes under matching prefixes; this is the reading half of that agreement, and the
/// only place it can be checked without a network.
/// </remarks>
[TestClass]
public class ClientUpdateSourceTests
{
    [TestCleanup]
    public void Cleanup() => Environment.SetEnvironmentVariable(ClientUpdateSource.EnvVarName, null);

    [TestMethod]
    public void The_two_flavours_never_read_the_same_feed()
    {
        // The failure this prevents is not subtle: a client pointed at the all-in-one's
        // feed replaces itself with the all-in-one on its next check.
        Assert.AreNotEqual(BakabaseUpdateSource.DefaultBaseUrl, ClientUpdateSource.DefaultBaseUrl);

        // Nor is either a prefix of the other, which would make one's per-RID folder
        // reachable underneath the other's.
        Assert.IsFalse(ClientUpdateSource.DefaultBaseUrl.StartsWith(BakabaseUpdateSource.DefaultBaseUrl,
            StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(BakabaseUpdateSource.DefaultBaseUrl.StartsWith(ClientUpdateSource.DefaultBaseUrl,
            StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void The_client_feed_matches_what_the_pipeline_publishes()
    {
        // Kept literal on purpose: _deploy.yml uploads to app/bakabase-client/releases/
        // and this is the other end of that. A rename on either side has to be a rename
        // on both, and a test naming the string is what makes that visible.
        Assert.AreEqual("https://cdn-public.anobaka.com/app/bakabase-client/releases/",
            ClientUpdateSource.DefaultBaseUrl);
    }

    [TestMethod]
    public void An_operator_can_point_the_client_somewhere_else_without_moving_the_server()
    {
        // Separate variable names: pointing one flavour at a private mirror must not
        // silently move the other.
        Assert.AreNotEqual(BakabaseUpdateSource.EnvVarName, ClientUpdateSource.EnvVarName);

        Environment.SetEnvironmentVariable(ClientUpdateSource.EnvVarName, "  http://mirror.local/feed/  ");
        Assert.AreEqual("http://mirror.local/feed/", new ClientUpdateSource().GetBaseUrl());

        Environment.SetEnvironmentVariable(ClientUpdateSource.EnvVarName, "   ");
        Assert.AreEqual(ClientUpdateSource.DefaultBaseUrl, new ClientUpdateSource().GetBaseUrl(),
            "a blank override must not blank out the feed");
    }
}
