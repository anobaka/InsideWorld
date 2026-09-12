import 'dart:convert';
import 'dart:math';
import 'dart:typed_data';

import 'package:crypto/crypto.dart';

/// Parsed form of an `Authorization: Bakabase-Device …` header.
class ParsedSignatureHeader {
  const ParsedSignatureHeader(this.deviceId, this.timestampSeconds, this.nonce, this.signature);

  final String deviceId;
  final int timestampSeconds;
  final String nonce;
  final String signature;
}

/// The wire format a paired device uses to prove who it is.
///
/// The Dart half of a contract with code that does not compile together: the
/// server verifies with `RemoteRequestSignature` on the C# side, the desktop
/// client's forwarding layer signs with it, and this reimplements it. Neither
/// side can be changed alone — trimming a trailing newline, re-encoding the
/// query or upper-casing something silently locks every device out — so both
/// are pinned to the same golden vectors rather than to each other's round
/// trip.
///
/// There is no transport encryption, so the long-term key never travels. Each
/// request carries an HMAC over a canonical description of itself instead,
/// which a passive listener can replay only inside a five-minute window and
/// only once, because the server remembers the nonce.
class RemoteRequestSignature {
  RemoteRequestSignature._();

  static const String scheme = 'Bakabase-Device';

  /// Version prefix of the canonical string, so the format can change later.
  static const String version = '1';

  /// How far a request's timestamp may sit from the server's clock. The app
  /// corrects for the offset using `serverTime` from the handshake, so this
  /// only has to absorb drift, not a wrong timezone.
  static const Duration maxClockSkew = Duration(minutes: 5);

  /// Bodies larger than this are signed as if they were empty. Buffering an
  /// upload twice to hash it costs more than the replay protection is worth;
  /// the method, path, query, timestamp and nonce are still covered.
  static const int maxHashedBodyBytes = 1024 * 1024;

  /// The exact bytes both sides run the HMAC over. Eight lines, always — the
  /// body digest line is present even for GET, carrying the empty string, so
  /// neither implementation needs a branch that the other might get wrong.
  ///
  /// [rawQuery] is the query string exactly as it goes on the wire, without
  /// the leading `?` and without re-encoding. Re-encoding is the classic way
  /// two implementations disagree: `/file/raw?fullname=` carries a whole path
  /// in there, and Dio writes a list as `ids=1&ids=2`.
  ///
  /// [bodyDigest] is [hashBody] of the request body, or the empty string when
  /// the body was not hashed (GET/HEAD, or larger than [maxHashedBodyBytes]).
  static String buildCanonicalString({
    required String deviceId,
    required String method,
    required String path,
    required String rawQuery,
    required int timestampSeconds,
    required String nonce,
    required String bodyDigest,
  }) =>
      '$version\n'
      '$deviceId\n'
      '${method.toUpperCase()}\n'
      '$path\n'
      '$rawQuery\n'
      '$timestampSeconds\n'
      '$nonce\n'
      '$bodyDigest';

  static String sign(List<int> key, String canonicalString) =>
      toBase64Url(Hmac(sha256, key).convert(utf8.encode(canonicalString)).bytes);

  /// Constant-time comparison, so a wrong signature cannot be narrowed down by
  /// timing how long the rejection took.
  static bool verify(List<int> key, String canonicalString, String signature) {
    if (signature.isEmpty) {
      return false;
    }

    final expected = utf8.encode(sign(key, canonicalString));
    final actual = utf8.encode(signature);

    // Length is not secret — it is fixed by the algorithm — but the comparison
    // still runs over every byte of the longer side.
    var difference = expected.length ^ actual.length;
    for (var i = 0; i < expected.length; i++) {
      difference |= expected[i] ^ (i < actual.length ? actual[i] : 0);
    }

    return difference == 0;
  }

  static String hashBody(List<int> body) => toBase64Url(sha256.convert(body).bytes);

  /// Renders the `Authorization` header value.
  ///
  /// The header is `Authorization` rather than something custom, and that is
  /// load-bearing on the server: response caching runs ahead of its auth
  /// filters, so a request carrying `Authorization` neither reads nor writes
  /// that cache. A thumbnail a paired device pulled from outside the libraries
  /// therefore cannot later be served to an anonymous caller.
  static String buildHeader(String deviceId, int timestampSeconds, String nonce, String signature) =>
      '$scheme $deviceId:$timestampSeconds:$nonce:$signature';

  /// Reads a header back. Returns null for anything that is not ours — an
  /// absent header, another scheme, a malformed value. A caller that is simply
  /// not paired is not an error, so this never throws.
  static ParsedSignatureHeader? tryParseHeader(String? headerValue) {
    if (headerValue == null || headerValue.trim().isEmpty) {
      return null;
    }

    final value = headerValue.trim();
    if (!value.toLowerCase().startsWith(scheme.toLowerCase())) {
      return null;
    }

    final payload = value.substring(scheme.length).trim();

    // The signature is base64url, so it never contains ':' — splitting into
    // exactly four is safe.
    final parts = payload.split(':');
    if (parts.length != 4) {
      return null;
    }

    if (parts[0].isEmpty || parts[2].isEmpty || parts[3].isEmpty) {
      return null;
    }

    final timestamp = int.tryParse(parts[1]);
    if (timestamp == null) {
      return null;
    }

    return ParsedSignatureHeader(parts[0], timestamp, parts[2], parts[3]);
  }

  /// base64url without padding: what fits in a header and a query string
  /// without further escaping.
  static String toBase64Url(List<int> bytes) =>
      base64Encode(bytes).replaceAll('=', '').replaceAll('+', '-').replaceAll('/', '_');

  static Uint8List fromBase64Url(String value) {
    final s = value.replaceAll('-', '+').replaceAll('_', '/');
    return base64Decode(s.padRight(s.length + (4 - s.length % 4) % 4, '='));
  }

  /// A fresh nonce. [Random.secure] rather than the default generator: a
  /// predictable nonce lets a listener pre-compute the one replay it is
  /// allowed.
  static String newNonce() {
    final random = Random.secure();

    return toBase64Url(List<int>.generate(12, (_) => random.nextInt(256)));
  }
}
