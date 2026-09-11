using Bakabase.Client.Remoting.Components.Diagnostics;
using Bakabase.Client.Remoting.Components.Forwarding;
using Bakabase.Infrastructures.Components.App;
using Microsoft.Extensions.Configuration;
using Bakabase.Infrastructures.Components.Gui;
using Bakabase.Infrastructures.Components.SystemService;
using Microsoft.Extensions.Hosting;

namespace Bakabase.Client.Remoting.Components;

/// <summary>
/// The thin client's host: the same startup the all-in-one uses, running a forwarding
/// layer instead of a server.
/// </summary>
/// <remarks>
/// <para>
/// Sharing <see cref="AppHost"/> rather than reimplementing it is what keeps the two
/// flavours behaving alike — single-instance handling, the exit flow, the splash and
/// error windows, options and logging all come from one place. What differs is stated
/// as overrides, and each of the three below is load-bearing.
/// </para>
/// </remarks>
public class ClientHost(IGuiAdapter guiAdapter, ISystemService systemService)
    : AppHost(guiAdapter, systemService)
{
    /// <summary>
    /// Different from the all-in-one's on purpose. The mutex and pipe names are derived
    /// from it, and a user is expected to run both at once — one serving their library,
    /// the other pointed at a server elsewhere. Sharing this identifier would let
    /// whichever started second mistake the other for a duplicate of itself and quit.
    /// </summary>
    protected override string? SingleInstanceId => "Bakabase.Client";

    /// <summary>
    /// Loopback only. This host answers whoever reaches it, with the device's signed
    /// access attached, so binding it to the network would put an unauthenticated door
    /// there.
    /// </summary>
    protected override string ListeningInterface => "127.0.0.1";

    /// <summary>
    /// One port, and the same one every launch. The browser keys localStorage, IndexedDB
    /// and its cache to the origin, port included, so a client that moved would look to
    /// the user like it had forgotten their settings.
    /// </summary>
    protected override IReadOnlyList<int>? OverrideListeningPorts() =>
        _port ??= [LoopbackPortAllocator.Allocate()];

    private IReadOnlyList<int>? _port;

    protected override string DisplayName => "Bakabase Client";

    protected override IHostBuilder CreateHostBuilder(params string[] args) =>
        AppUtils.CreateAppHostBuilder<ClientStartup>(args)
            .ConfigureAppConfiguration(builder => builder
                // Read from beside the executable rather than from the working directory,
                // which for a shortcut or a Dock launch is nothing in particular.
                .AddJsonFile(
                    Path.Combine(System.AppContext.BaseDirectory, ClientTelemetry.SettingsFileName),
                    optional: true)
                // Re-added because the file above was appended after the environment
                // variables the default builder registered, and last source wins. Without
                // this, the committed default would override the per-install override.
                .AddEnvironmentVariables());

    /// <summary>
    /// Nothing of the user's runs here — the tasks that must not be interrupted are on
    /// the server, and this process closing does not touch them.
    /// </summary>
    protected override Task<string?> CheckIfAppCanExitSafely() => Task.FromResult<string?>(null);
}
