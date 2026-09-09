import 'dart:convert';

import 'package:bakabase_mobile/core/request_signature.dart';
import 'package:flutter_test/flutter_test.dart';

/// The same golden vectors as RemoteRequestSignatureTests on the C# side.
///
/// Deliberately the same literals rather than a shared fixture: the point is
/// that two implementations that never link agree byte for byte, and a fixture
/// they both read could drift with them. The expected signatures were computed
/// independently (Python's hmac over the same canonical string), so both sides
/// check themselves against a third opinion rather than against each other.
void main() {
  // Bytes 0..31, matching the key the C# test hardcodes.
  final key = List<int>.generate(32, (i) => i);
  const timestamp = 1767225600;
  const nonce = 'bm9uY2U';
  const deviceId = 'dev-1';

  String canonical({
    String method = 'GET',
    String path = '/file/raw',
    String rawQuery = 'fullname=%2Fmedia%2Fa.mkv',
    String bodyDigest = '',
  }) =>
      RemoteRequestSignature.buildCanonicalString(
        deviceId: deviceId,
        method: method,
        path: path,
        rawQuery: rawQuery,
        timestampSeconds: timestamp,
        nonce: nonce,
        bodyDigest: bodyDigest,
      );

  group('canonical string', () {
    test('for a get is eight lines ending in an empty digest', () {
      final value = canonical();

      expect(value, '1\ndev-1\nGET\n/file/raw\nfullname=%2Fmedia%2Fa.mkv\n1767225600\nbm9uY2U\n');

      // Eight fields means seven separators, even though the last is empty.
      expect('\n'.allMatches(value).length, 7);
    });

    test('upper-cases the method but leaves the path and query untouched', () {
      final lower = canonical(method: 'post', path: '/Resource/Keys', rawQuery: 'ids=1&ids=2');
      final upper = canonical(method: 'POST', path: '/Resource/Keys', rawQuery: 'ids=1&ids=2');

      expect(lower, upper);

      // Case and repeated keys survive verbatim: re-encoding is how two
      // implementations end up disagreeing.
      expect(upper, contains('/Resource/Keys'));
      expect(upper, contains('ids=1&ids=2'));
    });
  });

  group('signing', () {
    test('matches the get vector', () {
      expect(RemoteRequestSignature.sign(key, canonical()), '-u9roe8ZZcjyaEoIJA3_OgFQHwVpb7NIQ8PA41wL5xU');
    });

    test('matches the post vector, covering the body', () {
      final digest = RemoteRequestSignature.hashBody(utf8.encode('{"ids":[1,2]}'));

      expect(digest, '7_nfIBPAihPN1_-r_PlRS29kEeKgtSxZSMcDbT1EIlM');

      final value = canonical(
        method: 'POST',
        path: '/player/batch-play',
        rawQuery: '',
        bodyDigest: digest,
      );

      expect(RemoteRequestSignature.sign(key, value), 'T2OE0S57oKu_mt0_VxwFJY8UAK5SMgfNujM2QmL0cso');
    });

    test('an empty body hashes to the sha256 of nothing', () {
      // Distinct from "not hashed", which is the empty string. A caller that
      // sends an empty body still commits to having sent one.
      expect(RemoteRequestSignature.hashBody(const []), '47DEQpj8HBSa-_TImW-5JCeuQeRkm5NMpJWZG3hSuFU');
    });
  });

  group('verification', () {
    test('accepts the right signature and rejects a tampered one', () {
      final value = canonical(rawQuery: 'fullname=a');
      final signature = RemoteRequestSignature.sign(key, value);

      expect(RemoteRequestSignature.verify(key, value, signature), isTrue);
      expect(RemoteRequestSignature.verify(key, canonical(rawQuery: 'fullname=b'), signature), isFalse);
      expect(RemoteRequestSignature.verify(List<int>.filled(32, 9), value, signature), isFalse);
      expect(RemoteRequestSignature.verify(key, value, ''), isFalse);

      // A truncated signature must not pass on a prefix match.
      expect(RemoteRequestSignature.verify(key, value, signature.substring(0, 10)), isFalse);
    });
  });

  group('header', () {
    test('round trips', () {
      final header = RemoteRequestSignature.buildHeader(deviceId, timestamp, nonce, 'sig');

      expect(header, 'Bakabase-Device dev-1:1767225600:bm9uY2U:sig');

      final parsed = RemoteRequestSignature.tryParseHeader(header);
      expect(parsed, isNotNull);
      expect(parsed!.deviceId, deviceId);
      expect(parsed.timestampSeconds, timestamp);
      expect(parsed.nonce, nonce);
      expect(parsed.signature, 'sig');
    });

    test('anything that is not ours parses to null rather than throwing', () {
      // Not being paired is the normal case, not an error.
      expect(RemoteRequestSignature.tryParseHeader(null), isNull);
      expect(RemoteRequestSignature.tryParseHeader('   '), isNull);
      expect(RemoteRequestSignature.tryParseHeader('Bearer abcdef'), isNull);
      expect(RemoteRequestSignature.tryParseHeader('Bakabase-Device dev-1:1767225600:nonce'), isNull);
      expect(RemoteRequestSignature.tryParseHeader('Bakabase-Device dev-1:notanumber:n:s'), isNull);
      expect(RemoteRequestSignature.tryParseHeader('Bakabase-Device ::n:s'), isNull);
      expect(RemoteRequestSignature.tryParseHeader('Bakabase-Device dev-1:1:n:'), isNull);
    });
  });

  group('base64url', () {
    test('round trips without padding or unsafe characters', () {
      for (final length in [1, 2, 3, 12, 31, 32]) {
        final bytes = List<int>.generate(length, (i) => (i * 7 + 3) & 0xff);
        final encoded = RemoteRequestSignature.toBase64Url(bytes);

        expect(encoded, isNot(contains('=')));
        expect(encoded, isNot(contains('+')));
        expect(encoded, isNot(contains('/')));
        expect(RemoteRequestSignature.fromBase64Url(encoded), bytes);
      }
    });

    test('decodes what the server encoded', () {
      // A device key arrives as base64url in the pairing response, so this
      // direction is the one that has to survive an unpadded string.
      expect(RemoteRequestSignature.fromBase64Url('47DEQpj8HBSa-_TImW-5JCeuQeRkm5NMpJWZG3hSuFU').length, 32);
    });
  });

  group('nonces', () {
    test('do not repeat', () {
      final nonces = List<String>.generate(500, (_) => RemoteRequestSignature.newNonce()).toSet();

      expect(nonces.length, 500);
    });
  });
}
