using System.Text.Json;
using System.Text.Json.Serialization;
using Bakabase.Infrastructures.Components.Gui;
using Bakabase.Infrastructures.Components.App;
using Bakabase.Infrastructures.Components.App.Upgrade;
using Bakabase.Infrastructures.Components.App.Upgrade.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bakabase.Client.Remoting.Components.Updating;

/// <summary>
/// The client updating itself, under <c>/client/updater/</c>.
/// </summary>
/// <remarks>
/// <para>
/// A separate surface from the server's <c>/updater/*</c> on purpose. Those routes are
/// forwarded, so the frontend's update banner keeps meaning what it always meant —
/// "update the server" — and the client's own version is a different question that only
/// this process can answer. Two products in one window, each updated where it lives.
/// </para>
/// <para>
/// The same <see cref="AppUpdater"/> the all-in-one uses, pointed at the client's feed by
/// <see cref="ClientUpdateSource"/>. Nothing about check, download or restart is
/// reimplemented here.
/// </para>
/// </remarks>
public static class ClientUpdaterEndpoints
{
    public const string Prefix = "/client/updater";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet($"{Prefix}/state",
            async (HttpContext context) =>
            {
                var updater = Resolve(context);

                await WriteAsync(context, updater?.State ?? NoUpdater);
            });

        endpoints.MapGet($"{Prefix}/new-version",
            async (HttpContext context) =>
            {
                var updater = Resolve(context);

                await WriteAsync(context, updater == null
                    ? new AppVersionInfo
                    {
                        RunningVersion = AppService.CoreVersion.ToString(),
                        UpdateCheckUnavailable = true
                    }
                    : await updater.CheckNewVersion());
            });

        endpoints.MapPost($"{Prefix}/update",
            async (HttpContext context) =>
            {
                var updater = Resolve(context);

                if (updater != null)
                {
                    await updater.StartUpdating();
                }

                await WriteAsync(context, updater?.State ?? NoUpdater);
            });

        endpoints.MapDelete($"{Prefix}/update",
            async (HttpContext context) =>
            {
                var updater = Resolve(context);
                updater?.StopUpdating();

                await WriteAsync(context, updater?.State ?? NoUpdater);
            });

        endpoints.MapPost($"{Prefix}/restart",
            async (HttpContext context, ILoggerFactory loggers) =>
            {
                var updater = Resolve(context);

                if (updater == null)
                {
                    await WriteAsync(context, NoUpdater);
                    return;
                }

                // Velopack hands over and ends in Environment.Exit, so the shell never gets
                // an orderly shutdown. Drop the tray icon while the app is still healthy, or
                // Windows keeps painting it beside the restarted instance's own icon until
                // the user happens to hover it. Same reasoning as the server's route.
                var tray = context.RequestServices.GetService<ITrayIconController>();
                tray?.SetTrayIconVisible(false);

                try
                {
                    await updater.ApplyUpdatesAndRestart();
                }
                catch (Exception e)
                {
                    // The call does not return on success, so arriving here means this
                    // process is staying alive after all — put the icon back.
                    tray?.SetTrayIconVisible(true);
                    loggers.CreateLogger(typeof(ClientUpdaterEndpoints))
                        .LogError(e, "Failed to apply the client update and restart");

                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    await WriteAsync(context, null, e.Message);
                }
            });
    }

    /// <summary>
    /// What a host with no updater reports.
    /// </summary>
    /// <remarks>
    /// <see cref="UpdaterStatus.Unavailable"/> already means "there is no install manifest
    /// to compare against", which is the truth here too. Deliberately not
    /// <see cref="UpdaterStatus.UpToDate"/>: claiming to be current without having asked
    /// anyone is the exact mistake that status was split out to stop.
    /// </remarks>
    private static UpdaterState NoUpdater => new() {Status = UpdaterStatus.Unavailable};

    /// <summary>
    /// The updater, or null in a host that cannot build one.
    /// </summary>
    /// <remarks>
    /// <see cref="AppUpdater"/> needs <see cref="AppService"/> and the application options,
    /// which every real client has — <c>AppHost</c> registers both — but a host assembled
    /// directly from <c>ClientStartup</c> does not. Rather than let that surface as a 500
    /// on a route whose whole job is to report status, it is reported as status.
    /// </remarks>
    private static AppUpdater? Resolve(HttpContext context)
    {
        try
        {
            return context.RequestServices.GetService<AppUpdater>();
        }
        catch (InvalidOperationException)
        {
            // Registered, but something it depends on is not.
            return null;
        }
    }

    private static async Task WriteAsync(HttpContext context, object? payload, string? message = null)
    {
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            code = context.Response.StatusCode == StatusCodes.Status200OK ? 0 : context.Response.StatusCode,
            message,
            data = payload
        }, Json), context.RequestAborted);
    }
}
