using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bakabase.Abstractions.Models.Domain.Constants;
using Bakabase.Client.Abstractions;
using Bakabase.Client.Abstractions.Models;
using Bakabase.Client.Components.Forwarding;
using Bakabase.Client.Components.UserMachine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AppContext = Bakabase.Infrastructures.Components.App.AppContext;

namespace Bakabase.Tests.RemoteAccess;

/// <summary>
/// The client's pipeline as it actually runs: a real listener, real requests.
/// </summary>
/// <remarks>
/// The guard and the forwarder have their own unit tests, but neither says whether they
/// are wired in the right order, or whether the guard was armed with the port the host
/// really bound. Those are exactly the mistakes that make a security check silently
/// protect nothing, so this drives the assembled thing over HTTP.
/// </remarks>
[TestClass]
public class ClientPipelineTests
{
    private IHost _host = null!;
    private int _port;
    private string _root = null!;

    private sealed class TempDirectory(string path) : IClientDataDirectory
    {
        public string Path => path;
        public string Ensure() => Directory.CreateDirectory(path).FullName;
    }

    [TestInitialize]
    public async Task Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "bakabase-client-pipeline", Guid.NewGuid().ToString("N"));
        _port = LoopbackPortAllocator.Allocate(45000);

        var address = $"http://127.0.0.1:{_port}";

        _host = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(web => web
                .UseUrls(address)
                .ConfigureServices(services =>
                {
                    // Registered before the startup's TryAdd, so the pipeline reads
                    // this rather than the real application data directory.
                    services.AddSingleton<IClientDataDirectory>(new TempDirectory(_root));

                    // The same object the real host publishes, and the only place the
                    // guard learns which port to expect.
                    services.AddSingleton(new AppContext
                    {
                        ListeningAddresses = [address],
                        ApiEndpoints = [address],
                        ApiEndpoint = address
                    });
                })
                .UseStartup<ClientStartup>())
            .Build();

        await _host.StartAsync();
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await _host.StopAsync();
        _host.Dispose();

        try
        {
            Directory.Delete(_root, true);
        }
        catch (Exception e) when (e is IOException or DirectoryNotFoundException)
        {
        }
    }

    /// <summary>
    /// Sends without letting HttpClient rewrite the Host header, which is the whole
    /// point of most of these.
    /// </summary>
    private async Task<HttpResponseMessage> Send(string path, string? host = null, string? origin = null,
        HttpMethod? method = null, string? body = null, string? accept = null, string? fetchMode = null)
    {
        // Redirects are the subject of some of these, so they are never followed.
        using var handler = new HttpClientHandler {AllowAutoRedirect = false};
        using var client = new HttpClient(handler);
        var request = new HttpRequestMessage(method ?? HttpMethod.Get, $"http://127.0.0.1:{_port}{path}");

        if (accept != null)
        {
            request.Headers.Add("Accept", accept);
        }

        if (fetchMode != null)
        {
            request.Headers.Add("Sec-Fetch-Mode", fetchMode);
        }

        request.Headers.Host = host ?? $"127.0.0.1:{_port}";

        if (origin != null)
        {
            request.Headers.Add("Origin", origin);
        }

        if (body != null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        return await client.SendAsync(request);
    }

    [TestMethod]
    public async Task A_rebound_hostname_never_reaches_the_pipeline()
    {
        var response = await Send("/resource/search", host: $"evil.com:{_port}");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.AreEqual(nameof(ClientForwardingFailure.ForeignCaller),
            response.Headers.GetValues("X-Bakabase-Client").First());
    }

    [TestMethod]
    public async Task A_cross_site_write_never_reaches_the_pipeline()
    {
        var response = await Send("/resource/search", origin: "https://evil.com", method: HttpMethod.Post);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task The_guard_is_armed_with_the_port_the_host_actually_bound()
    {
        // A guard built from a second copy of the port decision would either refuse
        // everything or protect nothing, and both look fine in a unit test.
        Assert.AreEqual(HttpStatusCode.OK, (await Send(ClientContextEndpoint.Path)).StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest,
            (await Send(ClientContextEndpoint.Path, host: $"127.0.0.1:{_port + 1}")).StatusCode);
    }

    [TestMethod]
    public async Task The_context_endpoint_answers_locally()
    {
        var response = await Send(ClientContextEndpoint.Path);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var data = body.GetProperty("data");

        // Answered without a server, which is the point: the UI has to render something
        // during a reconnect, and a context call that failed would leave it nothing.
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsFalse(data.GetProperty("serverReachable").GetBoolean());

        // Not the machine running the server. Saying otherwise would send the UI looking
        // for covers on listening ports that mean nothing here.
        Assert.IsFalse(data.GetProperty("isLocal").GetBoolean());

        Assert.AreEqual((int) ClientMode.PureClient, data.GetProperty("clientMode").GetInt32());
        Assert.IsTrue(data.GetProperty("cookieCaptureAvailable").GetBoolean());
    }

    [TestMethod]
    public async Task A_window_with_no_server_is_sent_somewhere_it_can_do_something()
    {
        // The whole point: a freshly installed client opens its window at this origin
        // with nothing attached. Answering that with a JSON refusal leaves the user
        // looking at machine-readable text and no way to enter an address.
        var response = await Send("/", accept: "text/html");

        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.AreEqual(ClientApiEndpoints.ConnectPath, response.Headers.Location?.ToString());
    }

    [TestMethod]
    public async Task The_connect_page_is_served_by_the_client_itself()
    {
        var response = await Send(ClientApiEndpoints.ConnectPath);
        var body = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.StartsWith(response.Content.Headers.ContentType?.MediaType!, "text/html");

        // It reaches the client's own API and nothing else — a page here that fetched
        // from the server would be asking the very thing that is not there yet.
        StringAssert.Contains(body, "/client/discover");
        StringAssert.Contains(body, "/client/status");
        StringAssert.Contains(body, "'/pair/code'");
        StringAssert.Contains(body, "'/pair/claim'");

        // Self-contained: no build step produced it, so nothing it needs can be missing
        // from an install.
        Assert.IsFalse(body.Contains("<script src", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(body.Contains("<link rel=\"stylesheet", StringComparison.OrdinalIgnoreCase));

        // Not cached: it hands over to the real frontend the moment a server is
        // attached, and a cached copy would keep greeting a client that is set up.
        Assert.IsTrue(response.Headers.CacheControl?.NoStore == true);
    }

    [TestMethod]
    public async Task A_fetch_with_no_server_still_gets_the_error_the_frontend_reads()
    {
        // Only a navigation is redirected. The frontend's own calls are written against
        // this envelope, and handing them a redirect to an HTML page instead would turn
        // a legible "not connected" into a parse failure.
        var response = await Send("/resource/search", accept: "application/json");

        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.AreEqual(nameof(ClientForwardingFailure.NotConnected),
            response.Headers.GetValues("X-Bakabase-Client").First());
    }

    [TestMethod]
    public async Task A_page_request_that_is_not_a_navigation_is_not_redirected()
    {
        // Sec-Fetch-Mode is what the browser itself says it is doing, and it beats
        // guessing from Accept.
        var response = await Send("/resource/search", accept: "text/html", fetchMode: "cors");

        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [TestMethod]
    public async Task A_path_that_looks_like_a_file_is_forwarded_like_anything_else()
    {
        // MapFallback's default pattern excludes paths whose last segment has a dot.
        // The server's whole frontend arrives that way — /assets/index-<hash>.js and
        // friends — so taking the default would 404 every asset and leave the window
        // blank on a client that had connected perfectly well.
        foreach (var path in new[]
                 {
                     "/favicon.ico",
                     "/assets/index-a1b2c3.js",
                     "/assets/index-a1b2c3.css",
                     "/tampermonkey/script/bakabase.user.js"
                 })
        {
            var response = await Send(path);

            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode, path);
            Assert.AreEqual(nameof(ClientForwardingFailure.NotConnected),
                response.Headers.GetValues("X-Bakabase-Client").First(), path);
        }
    }

    [TestMethod]
    public async Task Everything_else_says_there_is_no_server_yet()
    {
        var response = await Send("/resource/search");

        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.AreEqual(nameof(ClientForwardingFailure.NotConnected),
            response.Headers.GetValues("X-Bakabase-Client").First());

        // The same envelope shape the server's own gate uses, so the frontend has one
        // error format to understand rather than two.
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.IsTrue(body.TryGetProperty("message", out _));
    }

    [TestMethod]
    public async Task Nothing_is_written_to_disk_by_a_client_that_has_not_paired()
    {
        await Send(ClientContextEndpoint.Path);
        await Send("/resource/search");

        Assert.IsFalse(Directory.Exists(_root));
    }

    [TestMethod]
    public void Every_route_the_client_claims_has_a_handler()
    {
        // The dispatcher answers a declared route with no handler as "this client is
        // behind", which is right while one is being written and wrong once they all are.
        // Assembled from the real container, so a handler that was written but never
        // registered fails here rather than in front of a user.
        var dispatcher = _host.Services.GetRequiredService<UserMachineDispatcher>();
        var missing = UserMachineRoutes.All.Select(r => r.Key)
            .Except(dispatcher.ImplementedRoutes, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.AreEqual(0, missing.Length, string.Join(", ", missing));
    }

    [TestMethod]
    public async Task A_user_machine_route_is_intercepted_rather_than_forwarded()
    {
        // Playing a file is this machine's to do. Forwarding it would ask a server —
        // possibly somebody else's — to start a player on a screen nobody is watching.
        // It still fails here, since there is no server to ask what is playable, but it
        // fails as this machine's action: the forwarding header is absent, which is what
        // tells the two apart.
        var response = await Send("/resource/42/play");

        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.IsFalse(response.Headers.Contains("X-Bakabase-Client"));
    }

    [TestMethod]
    public async Task A_path_the_route_table_only_nearly_matches_still_goes_upstream()
    {
        // /resource/{id:int}/... has a constraint; a non-numeric segment is a different
        // route on the server and must not be swallowed here.
        var response = await Send("/player/playlist/latest/batch-play", method: HttpMethod.Post);

        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.AreEqual(nameof(ClientForwardingFailure.NotConnected),
            response.Headers.GetValues("X-Bakabase-Client").First());
    }

    [TestMethod]
    public async Task Opening_a_server_path_with_no_mapping_says_which_path_and_why()
    {
        // Nothing can infer where /data/media is on this machine, so the answer names
        // the path and carries a header the frontend keys its "set this up" prompt off.
        // Guessing, or reporting a generic failure, would both leave the user stuck.
        var response = await Send("/tool/open?path=%2Fdata%2Fmedia%2Fa.mkv");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.AreEqual(nameof(ClientForwardingFailure.PathNotMapped),
            response.Headers.GetValues("X-Bakabase-Client").First());

        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.AreEqual("/data/media/a.mkv", body.GetProperty("serverPath").GetString());
        StringAssert.Contains(body.GetProperty("message").GetString()!, "/data/media/a.mkv");
    }

    [TestMethod]
    public async Task The_clients_own_api_is_answered_here_not_forwarded()
    {
        // /client/ is a prefix the server does not use, so nothing under it can shadow
        // one of its routes or be shadowed later. With no server configured, a forwarded
        // request would have come back "not connected" instead.
        var response = await Send($"{ClientApiEndpoints.Prefix}/status");
        var data = JsonDocument.Parse(await response.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsFalse(response.Headers.Contains("X-Bakabase-Client"));
        Assert.AreEqual(0, data.GetProperty("servers").GetArrayLength());
        Assert.IsFalse(data.GetProperty("serverReachable").GetBoolean());

        // Says what this build can run here, so the settings page can tell the user
        // rather than letting them find out by clicking.
        Assert.IsTrue(data.GetProperty("implementedUserMachineRoutes").GetArrayLength() > 0);
    }

    [TestMethod]
    public async Task The_status_answer_never_carries_a_device_key()
    {
        // The key exists in this process to sign with. A page has no use for it, and
        // once it reaches one it is in browser memory, in devtools, and in any extension
        // the user has installed.
        var body = await (await Send($"{ClientApiEndpoints.Prefix}/status")).Content.ReadAsStringAsync();

        StringAssert.DoesNotMatch(body, new Regex("deviceKey", RegexOptions.IgnoreCase));
    }

    [TestMethod]
    public async Task Connecting_to_nothing_reports_it_rather_than_failing()
    {
        var response = await Send($"{ClientApiEndpoints.Prefix}/connect", method: HttpMethod.Post,
            body: "{\"address\":\"127.0.0.1:1\"}");

        var data = JsonDocument.Parse(await response.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual((int) ServerHandshakeOutcome.Unreachable, data.GetProperty("outcome").GetInt32());
    }

    [TestMethod]
    public async Task Opening_a_link_refuses_anything_that_is_not_a_web_address()
    {
        // Reached the local handler rather than the forwarder, and stopped there.
        var response = await Send("/gui/url?url=file%3A%2F%2F%2Fetc%2Fpasswd");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.IsFalse(response.Headers.Contains("X-Bakabase-Client"));
    }
}
