using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Bakabase.Client.Abstractions;
using Bakabase.Client.Components.Forwarding;
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
        HttpMethod? method = null)
    {
        using var client = new HttpClient();
        var request = new HttpRequestMessage(method ?? HttpMethod.Get, $"http://127.0.0.1:{_port}{path}");

        request.Headers.Host = host ?? $"127.0.0.1:{_port}";

        if (origin != null)
        {
            request.Headers.Add("Origin", origin);
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
    public async Task A_user_machine_route_is_intercepted_rather_than_forwarded()
    {
        // Playing a file is this machine's to do. Forwarding it would ask a server —
        // possibly somebody else's — to start a player on a screen nobody is watching,
        // and the answer would blame the server for something the client cannot do yet.
        var response = await Send("/resource/42/play");

        Assert.AreEqual(HttpStatusCode.NotImplemented, response.StatusCode);
        Assert.AreEqual(nameof(ClientForwardingFailure.NeedsNewerClient),
            response.Headers.GetValues("X-Bakabase-Client").First());
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
    public async Task Opening_a_link_refuses_anything_that_is_not_a_web_address()
    {
        // Reached the local handler rather than the forwarder, and stopped there.
        var response = await Send("/gui/url?url=file%3A%2F%2F%2Fetc%2Fpasswd");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.IsFalse(response.Headers.Contains("X-Bakabase-Client"));
    }
}
