using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Bakabase.Service.Components.RemoteAccess;

/// <summary>
/// Records which hub connections came from outside this machine, so
/// <see cref="RemoteConnectionRegistry"/> can hang up on them later.
/// </summary>
/// <remarks>
/// A filter rather than an override on the hub itself: the hub lives in the legacy
/// project and knows nothing about remote access, and a connection's standing is
/// decided by the middleware that ran during the handshake — this only reads what it
/// left behind.
/// </remarks>
public sealed class RemoteAccessHubFilter(RemoteConnectionRegistry registry) : IHubFilter
{
    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        var remote = context.Context.GetHttpContext()?.GetRemoteAccessContext();

        // Loopback is never tracked, so nothing here can ever hang up on the desktop
        // app's own UI.
        if (remote is {IsLoopback: false})
        {
            var connection = context.Context;
            registry.Track(remote.Device?.Id, connection.ConnectionId, connection.Abort);
        }

        await next(context);
    }

    public async Task OnDisconnectedAsync(HubLifetimeContext context, Exception? exception,
        Func<HubLifetimeContext, Exception?, Task> next)
    {
        registry.Forget(context.Context.ConnectionId);
        await next(context, exception);
    }
}
