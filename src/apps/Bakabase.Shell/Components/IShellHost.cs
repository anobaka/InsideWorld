using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Bakabase.Components;

/// <summary>
/// Everything the Avalonia shell needs from whatever is running behind it.
/// </summary>
/// <remarks>
/// The all-in-one supplies a host that owns the full server; a client flavour
/// supplies one that owns only the local forwarding layer. Keeping this surface to
/// three members is what lets <c>Bakabase.Shell</c> stay clear of
/// <c>Bakabase.Service</c> — the entry project is the only place that knows which
/// host it is starting.
/// </remarks>
public interface IShellHost : IDisposable
{
    /// <summary>
    /// The built generic host. Null until <see cref="Start"/> has produced one, and
    /// exit coordination has to tolerate that: the user can quit during startup.
    /// </summary>
    IHost? Host { get; }

    Task<bool> Start(string[] args);
}
