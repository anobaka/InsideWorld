using System;
using System.IO;
using System.Threading.Tasks;
using Bakabase.Modules.RemoteAccess.Abstractions.Components;
using Bakabase.Modules.RemoteAccess.Abstractions.Models;
using Bakabase.Modules.RemoteAccess.Components.Pairing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Bakabase.Tests.RemoteAccess;

[TestClass]
public class RemoteDeviceAuthenticatorTests
{
    private string _root = null!;
    private DateTime _now;
    private RemoteDeviceService _devices = null!;
    private NonceCache _nonces = null!;
    private RemoteDeviceAuthenticator _auth = null!;
    private PairingCredentials _device = null!;

    private sealed class TempDirectory(string path) : IRemoteAccessDataDirectory
    {
        public string Path => path;
        public string Ensure() => Directory.CreateDirectory(path).FullName;
    }

    [TestInitialize]
    public async Task Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "bakabase-auth-tests", Guid.NewGuid().ToString("N"));
        _now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var store = new RemoteDeviceStore(new TempDirectory(Path.Combine(_root, "remote-access")));
        _devices = new RemoteDeviceService(store, () => _now, () => "server-1");
        _nonces = new NonceCache(TimeSpan.FromMinutes(10), 4096, () => _now);
        _auth = new RemoteDeviceAuthenticator(_devices, _nonces, () => _now);

        var issue = await _devices.IssuePairingCodeAsync();
        _device = (await _devices.PairWithCodeAsync(issue.Code, "Phone", RemoteDevicePlatform.Android)).Credentials!;
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            Directory.Delete(_root, true);
        }
        catch (IOException)
        {
        }
    }

    private string SignedHeader(string method = "GET", string path = "/resource/search", string query = "",
        string bodyDigest = "", DateTime? at = null, string? nonce = null, string? deviceId = null,
        string? keyOverride = null)
    {
        var timestamp = new DateTimeOffset(at ?? _now).ToUnixTimeSeconds();
        var id = deviceId ?? _device.DeviceId;
        var n = nonce ?? RemoteRequestSignature.NewNonce();
        var canonical = RemoteRequestSignature.BuildCanonicalString(id, method, path, query, timestamp, n, bodyDigest);
        var key = RemoteRequestSignature.FromBase64Url(keyOverride ?? _device.Key);
        return RemoteRequestSignature.BuildHeader(id, timestamp, n, RemoteRequestSignature.Sign(key, canonical));
    }

    [TestMethod]
    public void A_correctly_signed_request_authenticates()
    {
        var result = _auth.Authenticate(SignedHeader(), "GET", "/resource/search", "", "");

        Assert.AreEqual(DeviceAuthOutcome.Authenticated, result.Outcome);
        Assert.AreEqual(_device.DeviceId, result.Device!.Id);
        Assert.IsTrue(result.MayProceed);
    }

    [TestMethod]
    public void No_header_is_anonymous_not_an_error()
    {
        // Every existing LAN phone sends nothing. That must stay a normal state, or
        // pairing would be a breaking change rather than an upgrade.
        foreach (var header in new[] {null, "", "   ", "Bearer whatever"})
        {
            var result = _auth.Authenticate(header, "GET", "/resource/search", "", "");
            Assert.AreEqual(DeviceAuthOutcome.Anonymous, result.Outcome, header ?? "null");
            Assert.IsTrue(result.MayProceed);
        }
    }

    [TestMethod]
    public async Task A_revoked_device_is_distinguishable_from_an_anonymous_caller()
    {
        var header = SignedHeader();
        await _devices.RevokeAsync(_device.DeviceId);

        var result = _auth.Authenticate(header, "GET", "/resource/search", "", "");

        Assert.AreEqual(DeviceAuthOutcome.UnknownDevice, result.Outcome);
        Assert.IsFalse(result.MayProceed);
    }

    [TestMethod]
    public void Changing_any_signed_field_breaks_the_signature()
    {
        var header = SignedHeader("GET", "/resource/search", "keyword=a");

        Assert.AreEqual(DeviceAuthOutcome.Authenticated,
            _auth.Authenticate(header, "GET", "/resource/search", "keyword=a", "").Outcome);

        // Each of these is a field an interceptor might want to rewrite.
        Assert.AreEqual(DeviceAuthOutcome.BadSignature,
            _auth.Authenticate(header, "POST", "/resource/search", "keyword=a", "").Outcome);
        Assert.AreEqual(DeviceAuthOutcome.BadSignature,
            _auth.Authenticate(header, "GET", "/resource/delete", "keyword=a", "").Outcome);
        Assert.AreEqual(DeviceAuthOutcome.BadSignature,
            _auth.Authenticate(header, "GET", "/resource/search", "keyword=b", "").Outcome);
        Assert.AreEqual(DeviceAuthOutcome.BadSignature,
            _auth.Authenticate(header, "GET", "/resource/search", "keyword=a", "somedigest").Outcome);
    }

    [TestMethod]
    public void A_signature_from_another_key_is_refused()
    {
        var other = RemoteRequestSignature.ToBase64Url(RemoteRequestSignature.NewDeviceKey());
        var header = SignedHeader(keyOverride: other);

        Assert.AreEqual(DeviceAuthOutcome.BadSignature,
            _auth.Authenticate(header, "GET", "/resource/search", "", "").Outcome);
    }

    [TestMethod]
    public void A_stale_or_future_timestamp_reports_the_clock_not_the_key()
    {
        // Sending someone to re-pair when their clock is wrong is the wrong fix, so the
        // two are reported differently.
        var old = SignedHeader(at: _now.AddMinutes(-6));
        Assert.AreEqual(DeviceAuthOutcome.Expired,
            _auth.Authenticate(old, "GET", "/resource/search", "", "").Outcome);

        var future = SignedHeader(at: _now.AddMinutes(6));
        Assert.AreEqual(DeviceAuthOutcome.Expired,
            _auth.Authenticate(future, "GET", "/resource/search", "", "").Outcome);

        var edge = SignedHeader(at: _now.AddMinutes(-4));
        Assert.AreEqual(DeviceAuthOutcome.Authenticated,
            _auth.Authenticate(edge, "GET", "/resource/search", "", "").Outcome);
    }

    [TestMethod]
    public void The_same_signature_works_once()
    {
        var header = SignedHeader();

        Assert.AreEqual(DeviceAuthOutcome.Authenticated,
            _auth.Authenticate(header, "GET", "/resource/search", "", "").Outcome);
        Assert.AreEqual(DeviceAuthOutcome.Replayed,
            _auth.Authenticate(header, "GET", "/resource/search", "", "").Outcome);
    }

    [TestMethod]
    public void A_failing_signature_does_not_burn_its_nonce()
    {
        // Otherwise an attacker who cannot forge a signature could still lock a device
        // out of nonces it is about to use.
        var nonce = "fixed-nonce";
        var wrongKey = RemoteRequestSignature.ToBase64Url(RemoteRequestSignature.NewDeviceKey());

        Assert.AreEqual(DeviceAuthOutcome.BadSignature,
            _auth.Authenticate(SignedHeader(nonce: nonce, keyOverride: wrongKey), "GET", "/resource/search", "", "")
                .Outcome);

        Assert.AreEqual(DeviceAuthOutcome.Authenticated,
            _auth.Authenticate(SignedHeader(nonce: nonce), "GET", "/resource/search", "", "").Outcome);
    }

    [TestMethod]
    public void A_body_digest_is_part_of_what_is_signed()
    {
        var digest = RemoteRequestSignature.HashBody("{\"a\":1}"u8);
        var header = SignedHeader("POST", "/resource/keys", "", digest);

        Assert.AreEqual(DeviceAuthOutcome.Authenticated,
            _auth.Authenticate(header, "POST", "/resource/keys", "", digest).Outcome);

        var tampered = RemoteRequestSignature.HashBody("{\"a\":2}"u8);
        Assert.AreEqual(DeviceAuthOutcome.BadSignature,
            _auth.Authenticate(header, "POST", "/resource/keys", "", tampered).Outcome);
    }

    [TestMethod]
    public async Task One_devices_nonce_does_not_block_another()
    {
        var issue = await _devices.IssuePairingCodeAsync();
        var second = (await _devices.PairWithCodeAsync(issue.Code, "Laptop", RemoteDevicePlatform.Windows))
            .Credentials!;

        const string nonce = "shared";

        Assert.AreEqual(DeviceAuthOutcome.Authenticated,
            _auth.Authenticate(SignedHeader(nonce: nonce), "GET", "/resource/search", "", "").Outcome);

        var header = SignedHeader(nonce: nonce, deviceId: second.DeviceId, keyOverride: second.Key);
        Assert.AreEqual(DeviceAuthOutcome.Authenticated,
            _auth.Authenticate(header, "GET", "/resource/search", "", "").Outcome);
    }
}
