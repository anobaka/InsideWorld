using Avalonia;
using Bakabase.Client.Components;
using Bakabase.Components;
using Bakabase.Infrastructures.Components.App;
using Bakabase.Infrastructures.Components.App.Upgrade;
using Velopack;

namespace Bakabase.Client.App;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // First statement, and it has to be. Everything below resolves the AppData
        // anchor, and the anchor is fixed the moment anything reads it — a directory
        // exists by then, and a log file is open inside it. Declaring the profile
        // afterwards would be refused, and rightly: this process would already have
        // adopted the all-in-one's database.
        AppDataAnchor.Use(AppDataPathProfile.Client);

        // Velopack must be the first thing to run in the app.
        // It handles install/uninstall/update lifecycle hooks.
        //
        // SetAutoApplyOnStartup(false): by default Velopack silently applies an
        // already-downloaded update on the next launch (before any UI shows).
        // We auto-download upgrade packages but want the install itself to be a
        // deliberate user action, so we disable that implicit apply.
        //
        // SetLogger: this logger is handed to the process-wide VelopackLocator, so every
        // later UpdateManager picks it up and its diagnostics land in AppLog instead of
        // only in Velopack's own log file.
        VelopackApp.Build()
            .SetAutoApplyOnStartup(false)
            .SetLogger(new SerilogVelopackLogger())
            .Run();

        // Everything from here on can be reported. Velopack's hook invocations never reach this
        // line (Run ends in Environment.Exit for them), so installing the handler after it costs
        // no coverage of a real launch.
        CrashHandler.Install();

        // Touching AppService runs its static constructor, which is what builds the Serilog file
        // sink. Pulling it forward leaves Avalonia's XAML load and tray-icon resolution inside
        // the window where a throw is reported rather than lost to the OS event log.
        _ = AppService.DefaultAppDataDirectory;

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Configure(Func<TApp>) rather than Configure<App>(): the shell takes the host it
    // should run behind as a constructor argument, and picking ClientHost here is
    // exactly what makes this build the client flavour.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure(() =>
                new global::Bakabase.App((guiAdapter, systemService) =>
                    new ClientShellHost(new ClientHost(guiAdapter, systemService))))
            .UsePlatformDetect()
            .LogToTrace();
}
