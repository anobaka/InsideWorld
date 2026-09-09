using System.Net;
using Bakabase.Abstractions.Models.Domain.Constants;
using Bakabase.Client.Components.Connection;
using Bakabase.Client.Components.Paths;
using Bakabase.Modules.Player.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Bakabase.Client.Components.UserMachine;

/// <summary>
/// Plays one item on this machine.
/// </summary>
/// <remarks>
/// <para>
/// The interesting case is a local file, and it has two ways to reach a player. If the
/// user has mapped that library, the player gets the real path and reads from disk —
/// fast, seekable, no server involved once it starts. If they have not, the player gets
/// a URL through this client's own forwarding layer instead, which signs and relays it.
/// </para>
/// <para>
/// That fallback is the difference between "set up a path mapping first" and "it plays".
/// Streaming is worse than local disk, so mapping is still worth doing and the settings
/// page says so — but nothing has to be configured before the first video plays, and a
/// library that genuinely is not on this machine works at all.
/// </para>
/// </remarks>
public sealed class PlayItemHandler(
    ActiveConnection connection,
    IUpstreamApi upstream,
    LocalPlayerResolver players,
    IShellOpener shell,
    ILoopbackAddressProvider loopback,
    ILogger<PlayItemHandler> logger) : PathHandlerBase(connection)
{
    public override string RouteKey => "GET /resource/{resourceId}/play-item";

    public override async Task HandleAsync(HttpContext context, IReadOnlyDictionary<string, string> routeValues)
    {
        if (!routeValues.TryGetValue("resourceId", out var idText) || !int.TryParse(idText, out var resourceId))
        {
            await WriteAsync(context, HttpStatusCode.BadRequest, "No resource id was given.");
            return;
        }

        var key = context.Request.Query["key"].ToString();

        if (!Enum.TryParse<DataOrigin>(context.Request.Query["origin"].ToString(), true, out var origin) ||
            string.IsNullOrWhiteSpace(key))
        {
            await WriteAsync(context, HttpStatusCode.BadRequest, "No item was given to play.");
            return;
        }

        var played = origin switch
        {
            DataOrigin.FileSystem or DataOrigin.Manual => await PlayFileAsync(context, resourceId, key),
            DataOrigin.Steam => await LaunchUriAsync(context, SteamUri(key)),
            DataOrigin.DLsite => await LaunchUriAsync(context,
                $"https://www.dlsite.com/maniax/work/=/product_id/{Uri.EscapeDataString(key)}.html"),
            DataOrigin.ExHentai => await LaunchUriAsync(context,
                $"https://exhentai.org/g/{key.TrimStart('/')}"),
            _ => await Unsupported(context, origin)
        };

        if (!played)
        {
            return;
        }

        // The server owns play history, so it is told after the fact — in the same
        // "{origin}:{key}" shape it writes when it plays something itself, or the two
        // halves would record the same event differently.
        await upstream.MarkPlayedAsync(resourceId, $"{origin}:{key}", context.RequestAborted);

        await WriteAsync(context, HttpStatusCode.OK, null);
    }

    private async Task<bool> PlayFileAsync(HttpContext context, int resourceId, string serverPath)
    {
        var mapped = ClientPathMapper.Map(serverPath, Connection.Server?.PathMappings ?? []);

        // A mapped file that is actually there beats streaming it back from the server
        // it already lives on. Mapped-but-missing means a stale mount, and streaming is
        // the honest fallback there too.
        var target = mapped.Mapped && File.Exists(mapped.LocalPath)
            ? mapped.LocalPath!
            : loopback.BuildRawFileUrl(serverPath);

        var options = await upstream.GetEffectivePlayerOptionsAsync(resourceId, context.RequestAborted);
        var player = players.Resolve(options, serverPath);

        try
        {
            if (player.IsSystemDefault)
            {
                shell.Launch(target);
            }
            else
            {
                shell.LaunchProcess(player.ExecutablePath!,
                    BatchPlayArguments.BuildFromTemplate(player.CommandTemplate, target),
                    player.NeedsShellExecute);
            }

            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to play {Target}", target);
            await WriteAsync(context, HttpStatusCode.InternalServerError,
                $"Could not start a player on your machine: {e.Message}");

            return false;
        }
    }

    private async Task<bool> LaunchUriAsync(HttpContext context, string? uri)
    {
        if (uri == null)
        {
            await WriteAsync(context, HttpStatusCode.BadRequest, "That item cannot be opened on your machine.");
            return false;
        }

        try
        {
            shell.Launch(uri);
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to open {Uri}", uri);
            await WriteAsync(context, HttpStatusCode.InternalServerError,
                $"Could not open it on your machine: {e.Message}");

            return false;
        }
    }

    /// <summary>
    /// Steam app ids are numbers, and this string reaches the OS as a URI to act on.
    /// Anything else is refused rather than handed over — the key came from a server
    /// that is not necessarily the user's.
    /// </summary>
    public static string? SteamUri(string key) =>
        key.Length > 0 && key.All(char.IsAsciiDigit) ? $"steam://rungameid/{key}" : null;

    private async Task<bool> Unsupported(HttpContext context, DataOrigin origin)
    {
        await WriteAsync(context, HttpStatusCode.NotImplemented,
            $"This client does not know how to play a {origin} item yet.");

        return false;
    }
}

/// <summary>
/// Where this client's own forwarding layer is listening, so it can hand a player a URL
/// that comes back through itself.
/// </summary>
public interface ILoopbackAddressProvider
{
    /// <summary>
    /// A URL a player on this machine can open for a file the server holds. It goes
    /// through the forwarding layer, which signs it — so the player needs no credentials
    /// and the device key never leaves this process.
    /// </summary>
    string BuildRawFileUrl(string serverPath);

    /// <summary>A URL on this client for any server path, e.g. a userscript to install.</summary>
    string BuildUrl(string pathAndQuery);
}
