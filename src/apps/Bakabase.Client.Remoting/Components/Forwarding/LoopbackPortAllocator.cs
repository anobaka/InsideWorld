using System.Net;
using System.Net.Sockets;

namespace Bakabase.Client.Remoting.Components.Forwarding;

/// <summary>
/// Picks the loopback port the forwarding layer listens on.
/// </summary>
/// <remarks>
/// Stability is the whole point, which is why this does not just ask the OS for a free
/// port. The browser keys localStorage, IndexedDB and every cached asset to the origin,
/// and the origin includes the port — so a client that moved from 34568 to 34569 between
/// launches would look to the user like it had forgotten their settings. It tries the
/// same port first every time and only walks forward when something else has it.
/// </remarks>
public static class LoopbackPortAllocator
{
    /// <summary>
    /// Deliberately not the server's own port: on a machine running both flavours they
    /// would otherwise fight, and the loser would silently move.
    /// </summary>
    public const int PreferredPort = 34568;

    /// <summary>How far to walk before giving up. Far enough to clear a crowd of stale listeners.</summary>
    public const int MaxAttempts = 32;

    /// <summary>
    /// The first free port at or after <paramref name="preferred"/>.
    /// </summary>
    /// <param name="isFree">
    /// Injected so the walk is testable without binding real sockets; defaults to
    /// actually trying to bind, which is the only answer that is not a guess.
    /// </param>
    public static int Allocate(int preferred = PreferredPort, Func<int, bool>? isFree = null)
    {
        isFree ??= CanBind;

        for (var offset = 0; offset < MaxAttempts; offset++)
        {
            var port = preferred + offset;

            if (port <= IPEndPoint.MaxPort && isFree(port))
            {
                return port;
            }
        }

        throw new IOException(
            $"No free loopback port between {preferred} and {preferred + MaxAttempts - 1}.");
    }

    private static bool CanBind(int port)
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, port));
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
