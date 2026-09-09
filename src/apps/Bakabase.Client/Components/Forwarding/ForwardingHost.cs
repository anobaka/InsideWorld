using System.Net;
using Bakabase.Client.Abstractions;
using Bakabase.Client.Components.Connection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Forwarder;

namespace Bakabase.Client.Components.Forwarding;

/// <summary>
/// The second HTTP host in this process: a loopback listener the embedded browser talks
/// to, which signs and relays to the real server.
/// </summary>
/// <remarks>
/// <para>
/// It exists so the frontend needs no notion of being remote. Everything it loads comes
/// from one origin on this machine, which keeps browser storage, the cover-sharding
/// trick and the hub connection working exactly as they do in the all-in-one — while the
/// device key stays in this process and never reaches a page.
/// </para>
/// <para>
/// Deliberately thin: no response caching, no compression, no buffering. Each of those
/// would sit between a video stream and the player, and the one thing this layer must
/// not do is get in the way of bytes it is only passing along.
/// </para>
/// </remarks>
public sealed class ForwardingHost(ClientHostOptions options)
{
    public int Port { get; private set; }

    /// <summary>The origin the embedded browser should open.</summary>
    public string BaseAddress => $"http://127.0.0.1:{Port}";

    public WebApplication Build()
    {
        Port = LoopbackPortAllocator.Allocate(options.PreferredPort);

        var builder = WebApplication.CreateBuilder();

        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            // Loopback only. Binding anything else would put an unauthenticated door
            // onto the network that hands out this device's signed access.
            kestrel.Listen(IPAddress.Loopback, Port);

            // The relayed request has already been through the server's own limits;
            // imposing a second, smaller one here would reject uploads that the server
            // would have accepted.
            kestrel.Limits.MaxRequestBodySize = null;
        });

        builder.Services.AddHttpForwarder();
        builder.Services.TryAddSingleton(options);
        builder.Services.TryAddSingleton<IClientDataDirectory>(options.DataDirectory);
        builder.Services.TryAddSingleton<IClientConnectionStore, ClientConnectionStore>();
        builder.Services.TryAddSingleton<ServerClock>();
        builder.Services.TryAddSingleton<ActiveConnection>();
        builder.Services.TryAddSingleton<IUpstreamTarget>(sp => sp.GetRequiredService<ActiveConnection>());
        builder.Services.TryAddSingleton<IClientCredentialProvider>(sp =>
            sp.GetRequiredService<ActiveConnection>());
        builder.Services.TryAddSingleton<UpstreamTransformer>();

        builder.Services.TryAddSingleton(_ => CreateUpstreamInvoker());
        builder.Services.TryAddSingleton<UpstreamForwarder>();

        // Its own HttpClient, signed, for the few calls the client makes on its own
        // behalf rather than on the browser's.
        builder.Services.AddHttpClient<IUpstreamContextProbe, UpstreamContextProbe>()
            .AddHttpMessageHandler(sp => new DeviceSigningHandler(
                sp.GetRequiredService<IClientCredentialProvider>(), sp.GetRequiredService<ServerClock>()));

        builder.Services.TryAddSingleton(sp => new ClientContextEndpoint(
            sp.GetRequiredService<ActiveConnection>(),
            sp.GetRequiredService<IUpstreamContextProbe>(),
            options.ClientVersion));

        var app = builder.Build();
        var guard = new LoopbackOriginGuard(Port);

        app.Use(async (context, next) =>
        {
            var verdict = guard.Evaluate(context.Request.Host.Value, context.Request.Headers.Origin.ToString(),
                context.Request.Method);

            if (verdict != LoopbackGuardVerdict.Allowed)
            {
                app.Logger.LogWarning("Refused {Verdict} request {Method} {Path} (Host: {Host}, Origin: {Origin})",
                    verdict, context.Request.Method, context.Request.Path, context.Request.Host.Value,
                    context.Request.Headers.Origin.ToString());

                context.Response.StatusCode = (int) HttpStatusCode.BadRequest;
                context.Response.Headers["X-Bakabase-Client"] = ClientForwardingFailure.ForeignCaller.ToString();
                await context.Response.WriteAsync("This address only serves Bakabase's own window.");
                return;
            }

            await next();
        });

        // Answered here rather than upstream: the question is about the client, and only
        // the client knows the answer.
        app.MapGet(ClientContextEndpoint.Path, (HttpContext context, ClientContextEndpoint endpoint) =>
            endpoint.WriteAsync(context));

        // Everything else is the server's. Actions that have to run on this machine are
        // still refused upstream, with a reason saying so, until the client learns to
        // run them itself — so nothing silently happens on the wrong computer in the
        // meantime.
        app.MapFallback((HttpContext context, UpstreamForwarder forwarder) => forwarder.ForwardAsync(context));

        return app;
    }

    /// <summary>
    /// The invoker YARP relays through. Everything that would normally be helpful is
    /// turned off: automatic decompression would break a byte-range video, cookies would
    /// mix the browser's with the client's, and redirect following would hide the
    /// server's own answer.
    /// </summary>
    private static HttpMessageInvoker CreateUpstreamInvoker() =>
        new(new SocketsHttpHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false,
            // Long-lived by design: this carries the hub connection and video streams.
            ConnectTimeout = TimeSpan.FromSeconds(15),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ActivityHeadersPropagator = null
        });
}

/// <summary>What the entry point tells the forwarding layer about itself.</summary>
public sealed record ClientHostOptions
{
    public required IClientDataDirectory DataDirectory { get; init; }

    public required string ClientVersion { get; init; }

    public int PreferredPort { get; init; } = LoopbackPortAllocator.PreferredPort;
}
