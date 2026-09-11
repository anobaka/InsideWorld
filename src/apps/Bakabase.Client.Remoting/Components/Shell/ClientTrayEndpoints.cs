using System.Text.Json;
using System.Text.Json.Serialization;
using Bakabase.Infrastructures.Components.Gui;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Bakabase.Client.Remoting.Components.Shell;

/// <summary>
/// The tray icon's running state, under <c>/client/tray</c>.
/// </summary>
/// <remarks>
/// <para>
/// In the all-in-one the task manager tells the tray directly, because both are in this
/// process. In a thin client the tasks are on the server and the tray is here, so the
/// state has to cross a machine boundary — and it already does: the window holds the
/// server's live task feed over SignalR, so the frontend reports what it sees rather than
/// this process opening a second connection to learn the same thing twice.
/// </para>
/// <para>
/// Which also decides the failure mode. The page reports on every change, mount included,
/// so a reload re-establishes the truth within a second; a window that died without
/// saying so leaves an icon claiming work that finished, and that is the one case this
/// cannot see. It is the acceptable half of the trade: the alternative is a second signed
/// hub connection kept open for the sake of an icon.
/// </para>
/// </remarks>
public static class ClientTrayEndpoints
{
    public const string Prefix = "/client/tray";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(Prefix, async (HttpContext context) =>
        {
            TrayStateInput? input;

            try
            {
                input = await JsonSerializer.DeserializeAsync<TrayStateInput>(context.Request.Body, Json,
                    context.RequestAborted);
            }
            catch (JsonException)
            {
                // No report is not a report of "idle": answering 200 to a caller that said
                // nothing would let a bug upstream quietly park the icon.
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await WriteAsync(context, new {applied = false});

                return;
            }

            if (input == null)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await WriteAsync(context, new {applied = false});

                return;
            }

            // Optional: a host with no GUI — a test host, or a client started headless —
            // has no tray to set, and that is not an error.
            var tray = context.RequestServices.GetService<ITrayIconController>();

            tray?.SetTrayIcon(input.Running);

            await WriteAsync(context, new {applied = tray != null});
        });
    }

    private static async Task WriteAsync(HttpContext context, object payload)
    {
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            code = context.Response.StatusCode == StatusCodes.Status200OK ? 0 : context.Response.StatusCode,
            data = payload
        }, Json), context.RequestAborted);
    }

    private sealed record TrayStateInput(bool Running);
}
