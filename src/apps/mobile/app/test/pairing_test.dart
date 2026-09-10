import 'dart:convert';

import 'package:bakabase_mobile/core/api_client.dart';
import 'package:bakabase_mobile/core/models.dart';
import 'package:bakabase_mobile/core/pairing.dart';
import 'package:bakabase_mobile/core/server_profiles.dart';
import 'package:bakabase_mobile/core/signing_interceptor.dart';
import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';

/// The parts of pairing that are wrong on a real phone as readily as on a fake one:
/// what gets signed, how the server's clock is read, and what an app that predates
/// pairing does with its stored profiles.
void main() {
  group('what gets signed', () {
    RequestOptions request(String method, {Object? data, String path = '/x'}) =>
        RequestOptions(path: path, method: method, data: data, baseUrl: 'http://h');

    test('GET and HEAD never hash a body', () {
      // The server keys off the method before it looks at anything else, so a GET
      // that somehow carried a body must still sign the empty digest.
      expect(DeviceSigningInterceptor.bodyDigest(request('GET', data: {'a': 1})), '');
      expect(DeviceSigningInterceptor.bodyDigest(request('HEAD', data: {'a': 1})), '');
      expect(DeviceSigningInterceptor.shouldHashBody('get', 10), isFalse);
    });

    test('an empty body is not hashed, which is not the same as hashing nothing', () {
      // The server's rule is ContentLength > 0. Hashing the empty string here instead
      // would fail every empty POST and nothing else — the sort of thing that only
      // turns up once someone taps the one button that sends one.
      expect(DeviceSigningInterceptor.shouldHashBody('POST', 0), isFalse);
      expect(DeviceSigningInterceptor.bodyDigest(request('POST')), '');
    });

    test('a body within the limit is hashed, and past it is not', () {
      expect(DeviceSigningInterceptor.shouldHashBody('POST', 1), isTrue);
      expect(DeviceSigningInterceptor.shouldHashBody('POST', 1024 * 1024), isTrue);
      expect(DeviceSigningInterceptor.shouldHashBody('POST', 1024 * 1024 + 1), isFalse);

      // Unknown length — a stream — is signed as though there were no body, which is
      // what the server does with a request whose length it cannot read either.
      expect(DeviceSigningInterceptor.shouldHashBody('POST', null), isFalse);
    });

    test('a json body hashes the bytes Dio will actually send', () {
      // Not a re-encoding of the map with different key order or spacing: the digest
      // has to be over the same bytes the server receives.
      const body = {'ids': [1, 2]};
      final digest = DeviceSigningInterceptor.bodyDigest(request('POST', data: body));

      expect(digest, isNotEmpty);
      expect(
        digest,
        DeviceSigningInterceptor.bodyDigest(request('POST', data: jsonEncode(body))),
        reason: 'the map and its own encoding must produce the same digest',
      );
    });
  });

  group("reading the server's clock", () {
    test('a timezone-less reading is taken as UTC', () {
      // The server sends DateTime.UtcNow through a serializer configured with
      // "yyyy-MM-dd HH:mm:ss.fff" — no offset, no Z. Dart would read that as local
      // time, so a phone in UTC+8 would measure an eight-hour offset and have every
      // signature it produced rejected as expired.
      final parsed = ServerInfo.parseServerTime('2026-09-10 03:56:04.123');

      expect(parsed, isNotNull);
      expect(parsed!.isUtc, isTrue);
      expect(parsed.hour, 3, reason: 'the wall-clock reading must be preserved as UTC');
    });

    test('a reading that does say what it is, is left alone', () {
      // So a server that starts sending proper ISO instants keeps working.
      expect(ServerInfo.parseServerTime('2026-09-10T03:56:04Z')!.hour, 3);
      expect(ServerInfo.parseServerTime('2026-09-10T11:56:04+08:00')!.toUtc().hour, 3);
    });

    test('nothing at all is not a clock reading', () {
      expect(ServerInfo.parseServerTime(null), isNull);
      expect(ServerInfo.parseServerTime(''), isNull);
      expect(ServerInfo.parseServerTime('not a time'), isNull);
    });
  });

  group('profiles written before pairing existed', () {
    test('load, and load as unpaired', () {
      // An upgrade must not strand someone on a "paired" flag that no key backs.
      final profile = ServerProfile.fromJson({
        'id': 'abc',
        'name': 'Desk-PC',
        'baseUrl': 'http://192.168.1.5:34567',
        'lastConnectedAt': '2026-01-01T00:00:00.000',
      });

      expect(profile, isNotNull);
      expect(profile!.paired, isFalse);
    });

    test('the flag round trips once it is set', () {
      final paired = ServerProfile(
        id: 'abc',
        name: 'Desk-PC',
        baseUrl: 'http://192.168.1.5:34567',
        lastConnectedAt: DateTime.utc(2026),
        paired: true,
      );

      expect(ServerProfile.fromJson(paired.toJson())!.paired, isTrue);
    });
  });

  group('pairing outcomes', () {
    test('the server\'s failure numbers map to what the user is told', () {
      expect(outcomeOf(1), PairingOutcome.codeRejected);
      expect(outcomeOf(2), PairingOutcome.requestRejected);
      expect(outcomeOf(3), PairingOutcome.awaitingApproval);
      expect(outcomeOf(4), PairingOutcome.tooManyAttempts);
    });

    test('no failure and no credentials is not success', () {
      // This is only reached when the response carried no key. Reporting paired
      // there would leave the app with nothing to store and no way to say why.
      expect(outcomeOf(0), isNot(PairingOutcome.paired));
      expect(outcomeOf(99), isNot(PairingOutcome.paired));
    });
  });

  group('denials', () {
    test('the pairing reasons are told apart, because each has a different fix', () {
      // Collapsing these into one "refused" sends people to the wrong fix — most
      // sharply for SignatureExpired, where the answer is the phone's clock and not
      // the pairing at all.
      expect(BakabaseApiClient.parseDenial('Unauthenticated'),
          RemoteAccessDenial.unauthenticated);
      expect(BakabaseApiClient.parseDenial('SignatureExpired'),
          RemoteAccessDenial.signatureExpired);
      expect(BakabaseApiClient.parseDenial('DeviceRevoked'),
          RemoteAccessDenial.deviceRevoked);
      expect(BakabaseApiClient.parseDenial('RunsOnUserMachine'),
          RemoteAccessDenial.runsOnUserMachine);
    });

    test('something a newer server invented is unknown, not a crash', () {
      expect(BakabaseApiClient.parseDenial('SomethingNew'), RemoteAccessDenial.unknown);
    });
  });
}
