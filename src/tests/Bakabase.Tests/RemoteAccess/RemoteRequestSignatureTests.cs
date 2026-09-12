using System;
using System.Linq;
using System.Text;
using Bakabase.Modules.RemoteAccess.Components.Pairing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Bakabase.Tests.RemoteAccess;

/// <summary>
/// Golden vectors for the request signature.
/// </summary>
/// <remarks>
/// This format is a contract with code that does not compile together: the desktop
/// client's forwarding layer signs with it, the server verifies with it, and the
/// mobile app reimplements it in Dart. A change that looks harmless here — trimming a
/// trailing newline, re-encoding the query, upper-casing something — silently locks
/// every device out. So the bytes are pinned, not just the round trip.
/// <para>
/// The expected signatures below were computed independently (Python's hmac over the
/// same canonical string), so they check this implementation rather than restate it.
/// </para>
/// </remarks>
[TestClass]
public class RemoteRequestSignatureTests
{
    /// <summary>Bytes 0..31, so the Dart side can hardcode the same key.</summary>
    private static readonly byte[] Key = Enumerable.Range(0, 32).Select(i => (byte) i).ToArray();

    private const long Timestamp = 1767225600;
    private const string Nonce = "bm9uY2U";
    private const string DeviceId = "dev-1";

    [TestMethod]
    public void Canonical_string_for_a_get_is_eight_lines_ending_in_an_empty_digest()
    {
        var canonical = RemoteRequestSignature.BuildCanonicalString(
            DeviceId, "GET", "/file/raw", "fullname=%2Fmedia%2Fa.mkv", Timestamp, Nonce, string.Empty);

        Assert.AreEqual(
            "1\ndev-1\nGET\n/file/raw\nfullname=%2Fmedia%2Fa.mkv\n1767225600\nbm9uY2U\n",
            canonical);

        // Eight fields means seven separators, even though the last field is empty.
        Assert.AreEqual(7, canonical.Count(c => c == '\n'));
    }

    [TestMethod]
    public void Signature_for_the_get_vector()
    {
        var canonical = RemoteRequestSignature.BuildCanonicalString(
            DeviceId, "GET", "/file/raw", "fullname=%2Fmedia%2Fa.mkv", Timestamp, Nonce, string.Empty);

        Assert.AreEqual("-u9roe8ZZcjyaEoIJA3_OgFQHwVpb7NIQ8PA41wL5xU",
            RemoteRequestSignature.Sign(Key, canonical));
    }

    [TestMethod]
    public void Signature_for_the_post_vector_covers_the_body()
    {
        var body = Encoding.UTF8.GetBytes("{\"ids\":[1,2]}");
        var digest = RemoteRequestSignature.HashBody(body);

        Assert.AreEqual("7_nfIBPAihPN1_-r_PlRS29kEeKgtSxZSMcDbT1EIlM", digest);

        var canonical = RemoteRequestSignature.BuildCanonicalString(
            DeviceId, "POST", "/player/batch-play", string.Empty, Timestamp, Nonce, digest);

        Assert.AreEqual("T2OE0S57oKu_mt0_VxwFJY8UAK5SMgfNujM2QmL0cso",
            RemoteRequestSignature.Sign(Key, canonical));
    }

    [TestMethod]
    public void An_empty_body_hashes_to_the_sha256_of_nothing()
    {
        // Distinct from "not hashed", which is the empty string. A caller that sends an
        // empty body still commits to having sent one.
        Assert.AreEqual("47DEQpj8HBSa-_TImW-5JCeuQeRkm5NMpJWZG3hSuFU",
            RemoteRequestSignature.HashBody([]));
    }

    [TestMethod]
    public void The_method_is_upper_cased_but_the_path_and_query_are_untouched()
    {
        var lower = RemoteRequestSignature.BuildCanonicalString(
            DeviceId, "post", "/Resource/Keys", "ids=1&ids=2", Timestamp, Nonce, string.Empty);
        var upper = RemoteRequestSignature.BuildCanonicalString(
            DeviceId, "POST", "/Resource/Keys", "ids=1&ids=2", Timestamp, Nonce, string.Empty);

        Assert.AreEqual(upper, lower);

        // Case and repeated keys in the query survive verbatim: re-encoding is how two
        // implementations end up disagreeing.
        StringAssert.Contains(upper, "/Resource/Keys");
        StringAssert.Contains(upper, "ids=1&ids=2");
    }

    [TestMethod]
    public void Verify_accepts_the_right_signature_and_rejects_a_tampered_one()
    {
        var canonical = RemoteRequestSignature.BuildCanonicalString(
            DeviceId, "GET", "/file/raw", "fullname=a", Timestamp, Nonce, string.Empty);
        var signature = RemoteRequestSignature.Sign(Key, canonical);

        Assert.IsTrue(RemoteRequestSignature.Verify(Key, canonical, signature));

        var tampered = RemoteRequestSignature.BuildCanonicalString(
            DeviceId, "GET", "/file/raw", "fullname=b", Timestamp, Nonce, string.Empty);
        Assert.IsFalse(RemoteRequestSignature.Verify(Key, tampered, signature));

        Assert.IsFalse(RemoteRequestSignature.Verify(RemoteRequestSignature.NewDeviceKey(), canonical, signature));
        Assert.IsFalse(RemoteRequestSignature.Verify(Key, canonical, string.Empty));
    }

    [TestMethod]
    public void Header_round_trips()
    {
        var header = RemoteRequestSignature.BuildHeader(DeviceId, Timestamp, Nonce, "sig");
        Assert.AreEqual("Bakabase-Device dev-1:1767225600:bm9uY2U:sig", header);

        var parsed = RemoteRequestSignature.TryParseHeader(header);
        Assert.IsNotNull(parsed);
        Assert.AreEqual(DeviceId, parsed!.DeviceId);
        Assert.AreEqual(Timestamp, parsed.TimestampSeconds);
        Assert.AreEqual(Nonce, parsed.Nonce);
        Assert.AreEqual("sig", parsed.Signature);
    }

    [TestMethod]
    public void A_header_that_is_not_ours_parses_to_null_rather_than_throwing()
    {
        // Not being paired is the normal case, not an error: every anonymous LAN
        // request reaches this code path.
        Assert.IsNull(RemoteRequestSignature.TryParseHeader(null));
        Assert.IsNull(RemoteRequestSignature.TryParseHeader("   "));
        Assert.IsNull(RemoteRequestSignature.TryParseHeader("Bearer abcdef"));
        Assert.IsNull(RemoteRequestSignature.TryParseHeader("Bakabase-Device dev-1:1767225600:nonce"));
        Assert.IsNull(RemoteRequestSignature.TryParseHeader("Bakabase-Device dev-1:notanumber:n:s"));
        Assert.IsNull(RemoteRequestSignature.TryParseHeader("Bakabase-Device ::n:s"));
        Assert.IsNull(RemoteRequestSignature.TryParseHeader("Bakabase-Device dev-1:1:n:"));
    }

    [TestMethod]
    public void Base64Url_round_trips_without_padding_or_unsafe_characters()
    {
        foreach (var length in new[] {1, 2, 3, 12, 31, 32})
        {
            var bytes = Enumerable.Range(0, length).Select(i => (byte) (i * 7 + 3)).ToArray();
            var encoded = RemoteRequestSignature.ToBase64Url(bytes);

            Assert.IsFalse(encoded.Contains('='), encoded);
            Assert.IsFalse(encoded.Contains('+'), encoded);
            Assert.IsFalse(encoded.Contains('/'), encoded);
            CollectionAssert.AreEqual(bytes, RemoteRequestSignature.FromBase64Url(encoded));
        }
    }

    [TestMethod]
    public void Generated_keys_and_nonces_do_not_repeat()
    {
        Assert.AreEqual(32, RemoteRequestSignature.NewDeviceKey().Length);

        var nonces = Enumerable.Range(0, 500).Select(_ => RemoteRequestSignature.NewNonce()).ToHashSet(StringComparer.Ordinal);
        Assert.AreEqual(500, nonces.Count);
    }
}
