using System.Net.Http;
using System.Net.Sockets;
using Bakabase.Abstractions.Exceptions;
using Bakabase.Infrastructures.Components.App;
using Bakabase.Infrastructures.Components.App.Models.Constants;
using Microsoft.Extensions.Configuration;

namespace Bakabase.Client.Components.Diagnostics;

/// <summary>
/// Error reporting for the client process.
/// </summary>
/// <remarks>
/// <para>
/// The same Sentry project as the server's, told apart by <see cref="LaunchModeTag"/>:
/// the two flavours share almost all of their code, so an error worth looking at is
/// usually worth looking at in both, and splitting them into two projects would hide
/// that a crash happens in one flavour and not the other.
/// </para>
/// <para>
/// The identity is not shared. The client keeps its own anonymous id under its own
/// application data directory, so one person running both flavours appears as two
/// installs — which is what they are, updated separately and capable of being on
/// different versions.
/// </para>
/// </remarks>
public static class ClientTelemetry
{
    /// <summary>
    /// Where the DSN comes from.
    /// </summary>
    /// <remarks>
    /// A key of its own rather than the server's <c>BackendDsn</c>, and that is a safety
    /// property, not a naming preference: the two flavours share an output directory in
    /// the test project, and reading the same key would have every test host that assembles
    /// this startup initialise the real SDK and report into the live project. The value is
    /// the same DSN; only the key that unlocks it is the client's own.
    /// </remarks>
    public const string DsnConfigurationKey = "Analytics:Sentry:ClientDsn";

    /// <summary>
    /// The client's own settings file, added to the configuration by <c>ClientHost</c>.
    /// </summary>
    /// <remarks>
    /// Not <c>appsettings.json</c>: the test project references this and
    /// <c>Bakabase.Service</c> together, and two files of that name overwrite one another
    /// in the output directory with nothing to say which won.
    /// </remarks>
    public const string SettingsFileName = "client-analytics.json";

    /// <summary>Tag every event carries, so client and server errors can be told apart.</summary>
    public const string LaunchModeTag = "launch_mode";

    public const string LaunchMode = "client";

    /// <summary>
    /// Whether this build has somewhere to report to. False for a development build and
    /// for a host assembled in a test, neither of which carries a DSN.
    /// </summary>
    public static bool IsEnabled(IConfiguration configuration, bool isDevelopment) =>
        !isDevelopment && !string.IsNullOrWhiteSpace(configuration[DsnConfigurationKey]);

    /// <summary>
    /// Starts reporting, or does nothing when <see cref="IsEnabled"/> says there is
    /// nowhere to report to.
    /// </summary>
    /// <returns>True when the SDK was initialised.</returns>
    public static bool Initialize(IConfiguration configuration, bool isDevelopment, string environmentName,
        string anonymousId)
    {
        if (!IsEnabled(configuration, isDevelopment))
        {
            return false;
        }

        var dsn = configuration[DsnConfigurationKey];

        SentrySdk.Init(o =>
        {
            o.Dsn = dsn;
            o.Release = AppService.CoreVersion.ToString();
            o.Environment = environmentName;
            // Errors only, same as the server: performance tracing would spend the quota
            // on a process whose entire job is to relay requests.
            o.TracesSampleRate = 0;
            // Off, and more emphatically here than on the server. Every request this
            // process makes goes to a server on somebody's LAN; a laptop closing its lid
            // would otherwise file an event per relayed call.
            o.CaptureFailedRequests = false;
            o.SetBeforeSend(@event => ShouldReport(@event.Exception) ? @event : null);
        });

        SentrySdk.ConfigureScope(scope =>
        {
            scope.SetTag(LaunchModeTag, LaunchMode);
            scope.SetTag("release_channel", ReleaseChannel(isDevelopment));
            scope.User = new SentryUser {Id = anonymousId};
        });

        return true;
    }

    /// <summary>
    /// Whether an exception is worth reporting.
    /// </summary>
    /// <remarks>
    /// A thin client lives on the far side of somebody's network, so the failures that
    /// would drown everything else are exactly the ones that are not defects: the server
    /// was asleep, the Wi-Fi dropped, the user closed the tab mid-stream.
    /// </remarks>
    public static bool ShouldReport(Exception? exception)
    {
        var ex = exception;

        while (ex != null)
        {
            switch (ex)
            {
                // The window navigated away or the stream was abandoned. Routine.
                case OperationCanceledException:
                // The server is not reachable. Also routine, and already reported to the
                // user by the forwarder in terms they can act on.
                case HttpRequestException:
                case SocketException:
                // Conditions the user resolves themselves — a missing dependency, an
                // expired cookie. Marked at the exception type, same as on the server.
                case IUserActionableException:
                    return false;
            }

            ex = ex.InnerException;
        }

        return true;
    }

    /// <summary>
    /// Which channel this build came from.
    /// </summary>
    /// <remarks>
    /// Mirrors the server's <c>ReleaseChannelDetector</c>, which lives in
    /// <c>Bakabase.Service</c> and is therefore out of reach here — the client references
    /// no part of the server. <c>ClientTelemetryTests</c> drives both and asserts they
    /// agree, so the copy cannot drift into reporting a different channel for the same
    /// build.
    /// </remarks>
    public static string ReleaseChannel(bool isDevelopment)
    {
        if (isDevelopment)
        {
            return "dev";
        }

        var version = AppService.CoreVersion;

        // The placeholder used before the first migration writes a real version.
        if (AppConstants.InitialSemVersion.Equals(version))
        {
            return "dev";
        }

        return string.IsNullOrEmpty(version.Prerelease) ? "stable" : "beta";
    }
}
