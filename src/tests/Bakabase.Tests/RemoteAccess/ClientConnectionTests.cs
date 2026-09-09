using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bakabase.Abstractions.Models.Domain.Constants;
using Bakabase.Client.Abstractions;
using Bakabase.Client.Abstractions.Models;
using Bakabase.Client.Components.Connection;
using Bakabase.Modules.RemoteAccess.Abstractions.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Bakabase.Tests.RemoteAccess;

/// <summary>
/// What the client makes of the answers a server can give it.
/// </summary>
/// <remarks>
/// Every outcome here sends the user somewhere different — check the address, update the
/// client, turn remote access on, wait for approval — so collapsing any two of them into
/// one message is a real cost, and these tests are what keeps them apart.
/// </remarks>
[TestClass]
public class ClientConnectionTests
{
    private StubHandler _handler = null!;
    private HttpClient _http = null!;
    private ServerClock _clock = null!;
    private ServerConnector _connector = null!;
    private string _root = null!;
    private ClientConnectionStore _store = null!;
    private ClientPairingService _pairing = null!;
    private DateTime _now;

    private sealed class TempDirectory(string path) : IClientDataDirectory
    {
        public string Path => path;
        public string Ensure() => Directory.CreateDirectory(path).FullName;
    }

    /// <summary>Answers whatever the test queued, and remembers what it was asked.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        public readonly List<HttpRequestMessage> Requests = [];
        public readonly List<string?> Bodies = [];
        public Func<HttpRequestMessage, HttpResponseMessage>? Respond;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken));

            return Respond?.Invoke(request) ?? throw new HttpRequestException("connection refused");
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) {Content = new StringContent(body, Encoding.UTF8, "application/json")};

    private static string ServerInfo(int protocolVersion = 1, RemoteAccessMode mode = RemoteAccessMode.Enabled,
        string? serverTime = null) =>
        "{\"code\":0,\"data\":{\"id\":\"server-1\",\"name\":\"Desk\",\"appVersion\":\"1.2.3\"," +
        $"\"protocolVersion\":{protocolVersion},\"mode\":\"{mode}\",\"pairingSupported\":true" +
        (serverTime == null ? "" : $",\"serverTime\":\"{serverTime}\"") +
        "}}";

    private static string PairingFailureBody(PairingFailure failure) =>
        $"{{\"code\":0,\"data\":{{\"failure\":\"{failure}\"}}}}";

    [TestInitialize]
    public void Setup()
    {
        _now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        _handler = new StubHandler();
        _http = new HttpClient(_handler);
        _clock = new ServerClock(() => _now);
        _connector = new ServerConnector(_http, _clock);

        _root = Path.Combine(Path.GetTempPath(), "bakabase-client-tests", Guid.NewGuid().ToString("N"));
        _store = new ClientConnectionStore(new TempDirectory(_root));
        _pairing = new ClientPairingService(_http, _store);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _http.Dispose();
        try
        {
            Directory.Delete(_root, true);
        }
        catch (Exception e) when (e is IOException or DirectoryNotFoundException)
        {
        }
    }

    // ---- addresses ----

    [TestMethod]
    public void An_address_typed_by_hand_is_filled_in()
    {
        // What a user reads off the settings page and what they type are not the same
        // string; both have to end up at the same server.
        Assert.AreEqual("http://192.168.1.5:34567", ServerConnector.Normalize("192.168.1.5:34567"));
        Assert.AreEqual("http://192.168.1.5:34567", ServerConnector.Normalize("http://192.168.1.5:34567/"));
        Assert.AreEqual("http://192.168.1.5:34567", ServerConnector.Normalize("  192.168.1.5:34567  "));
        Assert.AreEqual("https://bakabase.example.com", ServerConnector.Normalize("https://bakabase.example.com/"));
    }

    // ---- handshake ----

    [TestMethod]
    public async Task A_reachable_server_answers_with_its_identity()
    {
        _handler.Respond = _ => Json(ServerInfo());

        var result = await _connector.HandshakeAsync("192.168.1.5:34567");

        Assert.AreEqual(ServerHandshakeOutcome.Ok, result.Outcome);
        Assert.AreEqual("server-1", result.Server!.Id);
        Assert.AreEqual("Desk", result.Server.Name);
        Assert.AreEqual("http://192.168.1.5:34567/remote-access/server-info",
            _handler.Requests[0].RequestUri!.ToString());
    }

    [TestMethod]
    public async Task Nothing_listening_is_reported_as_unreachable_not_as_an_error()
    {
        // The connector is called from a settings page on every keystroke's worth of
        // address; throwing would make that a crash rather than a red hint.
        var result = await _connector.HandshakeAsync("192.168.1.5:34567");

        Assert.AreEqual(ServerHandshakeOutcome.Unreachable, result.Outcome);
    }

    [TestMethod]
    public async Task Something_else_listening_on_the_port_is_not_mistaken_for_a_server()
    {
        _handler.Respond = _ => Json("<html>hello</html>");
        Assert.AreEqual(ServerHandshakeOutcome.NotBakabase,
            (await _connector.HandshakeAsync("192.168.1.5:34567")).Outcome);

        _handler.Respond = _ => Json("""{"code":0,"data":{"name":"no id here"}}""");
        Assert.AreEqual(ServerHandshakeOutcome.NotBakabase,
            (await _connector.HandshakeAsync("192.168.1.5:34567")).Outcome);
    }

    [TestMethod]
    public async Task A_server_with_remote_access_off_says_so_rather_than_looking_broken()
    {
        // The gate refuses before the action runs, with the reason in a header. Reading
        // it is what turns "cannot connect" into "turn it on over there".
        var response = new HttpResponseMessage(HttpStatusCode.Forbidden);
        response.Headers.Add("X-Bakabase-Remote-Access", nameof(RemoteAccessDenialReason.Disabled));
        _handler.Respond = _ => response;

        Assert.AreEqual(ServerHandshakeOutcome.RemoteAccessDisabled,
            (await _connector.HandshakeAsync("192.168.1.5:34567")).Outcome);
    }

    [TestMethod]
    public async Task A_server_answering_Disabled_in_its_mode_is_treated_the_same()
    {
        _handler.Respond = _ => Json(ServerInfo(mode: RemoteAccessMode.Disabled));

        var result = await _connector.HandshakeAsync("192.168.1.5:34567");

        Assert.AreEqual(ServerHandshakeOutcome.RemoteAccessDisabled, result.Outcome);
        // Still reports who it is, so the UI can name the server it found.
        Assert.AreEqual("server-1", result.Server!.Id);
    }

    [TestMethod]
    public async Task A_version_mismatch_says_which_side_has_to_move()
    {
        // "Incompatible" would leave the user guessing which thing to update.
        _handler.Respond = _ => Json(ServerInfo(protocolVersion: ServerConnector.MaxSupportedProtocolVersion + 1));
        Assert.AreEqual(ServerHandshakeOutcome.ClientTooOld,
            (await _connector.HandshakeAsync("192.168.1.5:34567")).Outcome);

        _handler.Respond = _ => Json(ServerInfo(protocolVersion: ServerConnector.MinSupportedProtocolVersion - 1));
        Assert.AreEqual(ServerHandshakeOutcome.ServerTooOld,
            (await _connector.HandshakeAsync("192.168.1.5:34567")).Outcome);
    }

    [TestMethod]
    public async Task The_handshake_is_where_the_clock_offset_comes_from()
    {
        // Without this a machine with a wrong clock has every later request refused as
        // expired, which looks exactly like a broken pairing.
        _handler.Respond = _ => Json(ServerInfo(serverTime: "2026-01-01T12:07:00Z"));

        await _connector.HandshakeAsync("192.168.1.5:34567");

        Assert.AreEqual(7, Math.Round(_clock.Offset.TotalMinutes));
        Assert.AreEqual(_now.AddMinutes(7), _clock.Now);
    }

    [TestMethod]
    public async Task An_older_server_that_reports_no_time_leaves_the_clock_alone()
    {
        _handler.Respond = _ => Json(ServerInfo());

        await _connector.HandshakeAsync("192.168.1.5:34567");

        Assert.AreEqual(TimeSpan.Zero, _clock.Offset);
    }

    // ---- pairing ----

    [TestMethod]
    public async Task A_good_code_comes_back_with_credentials()
    {
        _handler.Respond = _ => Json("""{"code":0,"data":{"credentials":{"deviceId":"d1","key":"k1"},"failure":"None"}}""");

        var result = await _pairing.PairWithCodeAsync("192.168.1.5:34567", "123456");

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual("d1", result.Credentials!.DeviceId);
        StringAssert.Contains(_handler.Bodies[0]!, "123456");
    }

    [TestMethod]
    public async Task The_code_never_appears_in_the_url()
    {
        // It would land in access logs and proxy caches. The endpoint takes it in the
        // body for that reason, and the client has to hold up its end.
        _handler.Respond = _ => Json(PairingFailureBody(PairingFailure.CodeRejected));

        await _pairing.PairWithCodeAsync("192.168.1.5:34567", "123456");

        StringAssert.DoesNotMatch(_handler.Requests[0].RequestUri!.ToString(), new System.Text.RegularExpressions.Regex("123456"));
    }

    [TestMethod]
    public async Task Each_reason_the_server_gives_survives_the_trip()
    {
        foreach (var (failure, expected) in new[]
                 {
                     (PairingFailure.CodeRejected, ClientPairingOutcome.CodeRejected),
                     (PairingFailure.NotYetApproved, ClientPairingOutcome.AwaitingApproval),
                     (PairingFailure.RequestRejected, ClientPairingOutcome.RequestRejected),
                     (PairingFailure.TooManyAttempts, ClientPairingOutcome.TooManyAttempts)
                 })
        {
            _handler.Respond = _ => Json(PairingFailureBody(failure));

            Assert.AreEqual(expected, (await _pairing.ClaimAsync("192.168.1.5:34567", "req-1")).Outcome,
                failure.ToString());
        }
    }

    [TestMethod]
    public async Task Waiting_for_approval_is_an_answer_not_a_failure_to_retry_differently()
    {
        // The client polls this. It has to be able to tell "not yet" from "no", or it
        // would either give up early or keep asking after a rejection.
        _handler.Respond = _ => Json(PairingFailureBody(PairingFailure.NotYetApproved));
        Assert.AreEqual(ClientPairingOutcome.AwaitingApproval,
            (await _pairing.ClaimAsync("192.168.1.5:34567", "req-1")).Outcome);

        _handler.Respond = _ => Json(PairingFailureBody(PairingFailure.RequestRejected));
        Assert.AreEqual(ClientPairingOutcome.RequestRejected,
            (await _pairing.ClaimAsync("192.168.1.5:34567", "req-1")).Outcome);
    }

    [TestMethod]
    public async Task A_server_that_predates_pairing_says_so_instead_of_blaming_the_code()
    {
        _handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.NotFound);

        Assert.AreEqual(ClientPairingOutcome.PairingUnsupported,
            (await _pairing.PairWithCodeAsync("192.168.1.5:34567", "123456")).Outcome);
        Assert.AreEqual(ClientPairingOutcome.PairingUnsupported,
            (await _pairing.RequestPairingAsync("192.168.1.5:34567")).Outcome);
    }

    [TestMethod]
    public async Task A_filed_request_comes_back_with_its_ticket()
    {
        _handler.Respond = _ => Json("""{"code":0,"data":{"requestId":"req-1","expiresAt":"2026-01-01T12:10:00Z"}}""");

        var result = await _pairing.RequestPairingAsync("192.168.1.5:34567");

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual("req-1", result.Ticket!.RequestId);
    }

    [TestMethod]
    public async Task A_refused_request_reports_why_rather_than_looking_unreachable()
    {
        // The rate limiter answers through the same enum the rest of pairing uses, so a
        // throttled device is told to wait rather than to check its network.
        _handler.Respond = _ => Json(PairingFailureBody(PairingFailure.TooManyAttempts));

        var result = await _pairing.RequestPairingAsync("192.168.1.5:34567");

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(ClientPairingOutcome.TooManyAttempts, result.Outcome);
    }

    [TestMethod]
    public async Task This_device_introduces_itself_by_the_name_it_was_given()
    {
        await _store.MutateAsync(d =>
        {
            d.DeviceName = "Anobaka laptop";
            d.Platform = RemoteDevicePlatform.Windows;
        });

        _handler.Respond = _ => Json(PairingFailureBody(PairingFailure.CodeRejected));
        await _pairing.PairWithCodeAsync("192.168.1.5:34567", "123456");

        StringAssert.Contains(_handler.Bodies[0]!, "Anobaka laptop");
        StringAssert.Contains(_handler.Bodies[0]!, "Windows");
    }

    // ---- the connection store ----

    [TestMethod]
    public void Reading_a_client_that_has_never_paired_creates_nothing()
    {
        // The client ships pointed at nothing, and a user who opens it once and closes
        // it should not find a folder left behind.
        Assert.AreEqual(0, _store.Read().Servers.Count);
        Assert.IsFalse(Directory.Exists(_root));
    }

    [TestMethod]
    public async Task What_one_store_writes_another_reads_back()
    {
        await _store.MutateAsync(d =>
        {
            d.Servers.Add(new ClientServerConnection
            {
                ServerId = "server-1",
                BaseAddress = "http://192.168.1.5:34567",
                DeviceId = "d1",
                DeviceKey = "k1",
                PairedAt = _now
            });
            d.ActiveServerId = "server-1";
        });

        var reloaded = new ClientConnectionStore(new TempDirectory(_root)).Read();

        Assert.AreEqual("server-1", reloaded.ActiveServerId);
        Assert.AreEqual("k1", reloaded.Servers[0].DeviceKey);
    }

    [TestMethod]
    public async Task A_corrupt_file_does_not_stop_the_client_from_starting()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, ClientConnectionStore.FileName), "{ not json");

        // Pairing again is visible and recoverable; refusing to launch is neither.
        Assert.AreEqual(0, new ClientConnectionStore(new TempDirectory(_root)).Read().Servers.Count);
    }

    [TestMethod]
    public async Task No_temporary_file_survives_a_write()
    {
        await _store.MutateAsync(d => d.ActiveServerId = "server-1");

        Assert.AreEqual(0, Directory.GetFiles(_root, "*.tmp").Length);
    }
}
