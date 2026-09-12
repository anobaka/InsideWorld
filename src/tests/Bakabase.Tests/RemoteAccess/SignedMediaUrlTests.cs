using System;
using System.IO;
using System.Threading.Tasks;
using Bakabase.Modules.RemoteAccess.Abstractions.Components;
using Bakabase.Modules.RemoteAccess.Abstractions.Models;
using Bakabase.Modules.RemoteAccess.Components.Pairing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Bakabase.Tests.RemoteAccess;

/// <summary>
/// The URL a paired device hands to something that cannot sign — a native player, an
/// <c>&lt;img&gt;</c> tag.
/// </summary>
[TestClass]
public class SignedMediaUrlTests
{
    private string _root = null!;
    private DateTime _now;
    private RemoteDeviceService _devices = null!;
    private RemoteDeviceAuthenticator _auth = null!;
    private PairingCredentials _device = null!;
    private byte[] _key = null!;

    private sealed class TempDirectory(string path) : IRemoteAccessDataDirectory
    {
        public string Path => path;
        public string Ensure() => Directory.CreateDirectory(path).FullName;
    }

    [TestInitialize]
    public async Task Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "bakabase-signed-url-tests", Guid.NewGuid().ToString("N"));
        _now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var store = new RemoteDeviceStore(new TempDirectory(Path.Combine(_root, "remote-access")));
        _devices = new RemoteDeviceService(store, () => _now);
        _auth = new RemoteDeviceAuthenticator(_devices, new NonceCache(TimeSpan.FromMinutes(10), 4096, () => _now),
            () => _now);

        var issue = await _devices.IssuePairingCodeAsync();
        _device = (await _devices.PairWithCodeAsync(issue.Code, "Phone", RemoteDevicePlatform.Android)).Credentials!;
        _key = RemoteRequestSignature.FromBase64Url(_device.Key);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            Directory.Delete(_root, true);
        }
        catch (Exception e) when (e is IOException or DirectoryNotFoundException)
        {
        }
    }

    private string Token(string path = "/tool/thumbnail", string query = "path=%2Fmedia%2Fa.jpg&w=600",
        DateTime? expiresAt = null, byte[]? key = null, string? deviceId = null) =>
        SignedMediaUrl.BuildToken(key ?? _key, deviceId ?? _device.DeviceId, path, query,
            expiresAt ?? _now.Add(SignedMediaUrl.DefaultLifetime));

    [TestMethod]
    public void A_correctly_signed_url_authenticates()
    {
        var token = Token();
        var result = _auth.AuthenticateSignedUrl(token, "GET", "/tool/thumbnail",
            $"path=%2Fmedia%2Fa.jpg&w=600&bkbt={token}");

        Assert.AreEqual(DeviceAuthOutcome.Authenticated, result.Outcome);
        Assert.AreEqual(_device.DeviceId, result.Device!.Id);
    }

    [TestMethod]
    public void BuildUrl_produces_something_the_server_accepts()
    {
        // The client builds the whole URL, so the round trip is the contract: whatever
        // BuildUrl emits has to verify as-is.
        var url = SignedMediaUrl.BuildUrl(_key, _device.DeviceId, "/tool/thumbnail", "path=%2Fmedia%2Fa.jpg&w=600",
            _now.AddHours(1));

        var split = url.IndexOf('?');
        var path = url[..split];
        var query = url[(split + 1)..];
        var token = query[(query.LastIndexOf('=') + 1)..];

        Assert.AreEqual(DeviceAuthOutcome.Authenticated,
            _auth.AuthenticateSignedUrl(token, "GET", path, query).Outcome);
    }

    [TestMethod]
    public void No_token_is_anonymous_not_an_error()
    {
        foreach (var token in new[] {null, "", "   ", "garbage", "bkb-url-1.only.three"})
        {
            Assert.AreEqual(DeviceAuthOutcome.Anonymous,
                _auth.AuthenticateSignedUrl(token, "GET", "/tool/thumbnail", "").Outcome, token ?? "null");
        }
    }

    [TestMethod]
    public void The_token_covers_the_whole_url()
    {
        var token = Token();

        // Each of these is a different file or a different endpoint. None may reuse the
        // token for the one that was signed.
        Assert.AreEqual(DeviceAuthOutcome.BadSignature,
            _auth.AuthenticateSignedUrl(token, "GET", "/file/raw", $"path=%2Fmedia%2Fa.jpg&w=600&bkbt={token}")
                .Outcome);
        Assert.AreEqual(DeviceAuthOutcome.BadSignature,
            _auth.AuthenticateSignedUrl(token, "GET", "/tool/thumbnail", $"path=%2Fmedia%2Fb.jpg&w=600&bkbt={token}")
                .Outcome);
        Assert.AreEqual(DeviceAuthOutcome.BadSignature,
            _auth.AuthenticateSignedUrl(token, "GET", "/tool/thumbnail", $"path=%2Fmedia%2Fa.jpg&bkbt={token}")
                .Outcome);
    }

    [TestMethod]
    public void A_link_cannot_become_a_write()
    {
        var token = Token();

        foreach (var method in new[] {"POST", "PUT", "DELETE", "PATCH"})
        {
            Assert.AreEqual(DeviceAuthOutcome.BadSignature,
                _auth.AuthenticateSignedUrl(token, method, "/tool/thumbnail",
                    $"path=%2Fmedia%2Fa.jpg&w=600&bkbt={token}").Outcome, method);
        }

        // HEAD is a GET without a body — players use it to probe length before a range
        // request, so refusing it would break playback.
        Assert.AreEqual(DeviceAuthOutcome.Authenticated,
            _auth.AuthenticateSignedUrl(token, "HEAD", "/tool/thumbnail",
                $"path=%2Fmedia%2Fa.jpg&w=600&bkbt={token}").Outcome);
    }

    [TestMethod]
    public void The_same_url_can_be_fetched_over_and_over()
    {
        // Playback issues dozens of range requests against one URL. Treating the second
        // as a replay would break it.
        var token = Token();
        var query = $"path=%2Fmedia%2Fa.jpg&w=600&bkbt={token}";

        for (var i = 0; i < 5; i++)
        {
            Assert.AreEqual(DeviceAuthOutcome.Authenticated,
                _auth.AuthenticateSignedUrl(token, "GET", "/tool/thumbnail", query).Outcome);
        }
    }

    [TestMethod]
    public void A_lapsed_token_is_refused()
    {
        var token = Token(expiresAt: _now.AddMinutes(-1));

        Assert.AreEqual(DeviceAuthOutcome.Expired,
            _auth.AuthenticateSignedUrl(token, "GET", "/tool/thumbnail",
                $"path=%2Fmedia%2Fa.jpg&w=600&bkbt={token}").Outcome);
    }

    [TestMethod]
    public void A_token_minted_to_last_forever_is_refused()
    {
        // The client picks the lifetime, so without a cap a device could hand out what
        // amounts to a permanent key.
        var token = Token(expiresAt: _now.Add(SignedMediaUrl.MaxLifetime).AddDays(1));

        Assert.AreEqual(DeviceAuthOutcome.Expired,
            _auth.AuthenticateSignedUrl(token, "GET", "/tool/thumbnail",
                $"path=%2Fmedia%2Fa.jpg&w=600&bkbt={token}").Outcome);
    }

    [TestMethod]
    public async Task Revoking_a_device_kills_links_it_already_handed_out()
    {
        var token = Token();
        await _devices.RevokeAsync(_device.DeviceId);

        Assert.AreEqual(DeviceAuthOutcome.UnknownDevice,
            _auth.AuthenticateSignedUrl(token, "GET", "/tool/thumbnail",
                $"path=%2Fmedia%2Fa.jpg&w=600&bkbt={token}").Outcome);
    }

    [TestMethod]
    public void A_signature_from_another_key_is_refused()
    {
        var token = Token(key: RemoteRequestSignature.NewDeviceKey());

        Assert.AreEqual(DeviceAuthOutcome.BadSignature,
            _auth.AuthenticateSignedUrl(token, "GET", "/tool/thumbnail",
                $"path=%2Fmedia%2Fa.jpg&w=600&bkbt={token}").Outcome);
    }

    [TestMethod]
    public void A_header_signature_cannot_be_replayed_as_a_url_token()
    {
        // Both formats are signed with the same key, so the version line is the only
        // thing keeping them apart. This is that line's test.
        const long expiry = 1_800_000_000;

        var asHeader = RemoteRequestSignature.BuildCanonicalString(_device.DeviceId, "GET", "/tool/thumbnail",
            "path=%2Fmedia%2Fa.jpg", expiry, "nonce", "");
        var asUrl = SignedMediaUrl.BuildCanonicalString(_device.DeviceId, "GET", "/tool/thumbnail",
            "path=%2Fmedia%2Fa.jpg", expiry);

        Assert.AreNotEqual(RemoteRequestSignature.Sign(_key, asHeader), RemoteRequestSignature.Sign(_key, asUrl));
    }

    [TestMethod]
    public void StripToken_removes_only_the_token()
    {
        Assert.AreEqual("path=a&w=600", SignedMediaUrl.StripToken("path=a&w=600&bkbt=xyz"));
        Assert.AreEqual("path=a&w=600", SignedMediaUrl.StripToken("bkbt=xyz&path=a&w=600"));
        Assert.AreEqual("path=a&w=600", SignedMediaUrl.StripToken("path=a&bkbt=xyz&w=600"));
        Assert.AreEqual("path=a&w=600", SignedMediaUrl.StripToken("path=a&w=600"));
        Assert.AreEqual("", SignedMediaUrl.StripToken(""));

        // A value that merely contains the key's letters is not the key.
        Assert.AreEqual("path=bkbt&bkbtx=1", SignedMediaUrl.StripToken("path=bkbt&bkbtx=1&bkbt=xyz"));
    }
}
