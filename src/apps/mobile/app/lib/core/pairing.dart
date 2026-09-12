import 'dart:io';

import 'package:dio/dio.dart';

import 'credentials.dart';

/// Why pairing did not produce credentials.
///
/// Mirrors PairingFailure on the C# side, which travels as a number — the server
/// serializes enums numerically. [unreachable] has no server-side counterpart: it is
/// what this app reports when nothing answered at all.
enum PairingOutcome {
  paired,
  codeRejected,
  requestRejected,
  awaitingApproval,
  tooManyAttempts,
  unreachable,
  unsupported,
}

/// Server-side PairingFailure values, by their numbers.
///
/// Note the absence of a mapping for 0: "no failure" is only ever reported alongside
/// credentials, and this is called when there are none. Treating it as success there
/// would hand the caller a paired outcome with nothing to store — which the UI would
/// act on by going nowhere, quietly.
PairingOutcome outcomeOf(int failure) => switch (failure) {
      1 => PairingOutcome.codeRejected,
      2 => PairingOutcome.requestRejected,
      3 => PairingOutcome.awaitingApproval,
      4 => PairingOutcome.tooManyAttempts,
      // 0 with no credentials, or something a newer server invented. Either way the
      // one thing it certainly is not is a key.
      _ => PairingOutcome.requestRejected,
    };

class PairingResult {
  const PairingResult(this.outcome, {this.credentials, this.requestId});

  final PairingOutcome outcome;

  /// Present only when [outcome] is [PairingOutcome.paired].
  final DeviceCredentials? credentials;

  /// Present when a request was filed and is waiting for someone to approve it.
  final String? requestId;
}

/// The three ways a phone gets a key, all of them reachable without one.
///
/// Pairing is the one exchange that happens before this device can sign anything, so
/// it uses a plain Dio with no interceptor. That is also why the server rate-limits
/// the request endpoint by address: it is the only thing an uncredentialed caller can
/// make it write to disk.
class PairingService {
  PairingService(this.baseUrl, {Dio? dio})
      : _dio = dio ??
            Dio(BaseOptions(
              baseUrl: baseUrl,
              connectTimeout: const Duration(seconds: 5),
              receiveTimeout: const Duration(seconds: 15),
              validateStatus: (_) => true,
            ));

  final String baseUrl;
  final Dio _dio;

  /// What the server records as this device's platform, so a person approving a
  /// request can tell which of their devices is asking.
  static int get currentPlatform {
    if (Platform.isAndroid) return 4;
    if (Platform.isIOS) return 5;
    if (Platform.isWindows) return 1;
    if (Platform.isMacOS) return 2;
    if (Platform.isLinux) return 3;

    return 0;
  }

  Future<PairingResult> pairWithCode(String code, String deviceName) =>
      _pair('/remote-access/pair/code', {
        'code': code.trim(),
        'deviceName': deviceName,
        'platform': currentPlatform,
      });

  /// Files a request for somebody at an already-paired device to approve.
  Future<PairingResult> requestPairing(String deviceName) async {
    final data = await _post('/remote-access/pair/request', {
      'deviceName': deviceName,
      'platform': currentPlatform,
    });

    if (data == null) {
      return PairingResult(_lastOutcome);
    }

    final requestId = data['requestId'] as String?;

    if (requestId == null || requestId.isEmpty) {
      return PairingResult(outcomeOf((data['failure'] as num?)?.toInt() ?? 0));
    }

    return PairingResult(PairingOutcome.awaitingApproval, requestId: requestId);
  }

  /// Collects the credentials an approval produced. Answers
  /// [PairingOutcome.awaitingApproval] until somebody approves, which is the normal
  /// state — the caller polls.
  Future<PairingResult> claim(String requestId) =>
      _pair('/remote-access/pair/claim', {'requestId': requestId});

  Future<PairingResult> _pair(String path, Map<String, dynamic> body) async {
    final data = await _post(path, body);

    if (data == null) {
      return PairingResult(_lastOutcome);
    }

    final credentials = data['credentials'];

    if (credentials is Map<String, dynamic>) {
      final parsed = DeviceCredentials.fromJson(credentials);

      if (parsed != null) {
        return PairingResult(PairingOutcome.paired, credentials: parsed);
      }
    }

    return PairingResult(outcomeOf((data['failure'] as num?)?.toInt() ?? 0));
  }

  /// Why the last [_post] produced nothing. Read only when it did.
  PairingOutcome _lastOutcome = PairingOutcome.unreachable;

  /// Posts and unwraps the `{code, data}` envelope, or null when the server could not
  /// be reached or did not answer in that shape. [_lastOutcome] says which.
  Future<Map<String, dynamic>?> _post(String path, Map<String, dynamic> body) async {
    _lastOutcome = PairingOutcome.unreachable;

    final Response<dynamic> response;

    try {
      response = await _dio.post<dynamic>(path, data: body);
    } on DioException {
      return null;
    }

    final status = response.statusCode ?? 0;

    // 404 is what a server from before pairing answers: it has no such route. Saying
    // "the server stopped answering" there would send the user to check their network
    // when the fix is to update the server.
    if (status == 404) {
      _lastOutcome = PairingOutcome.unsupported;
      return null;
    }

    if (status < 200 || status >= 300) {
      return null;
    }

    final data = response.data;

    return data is Map<String, dynamic> && data['data'] is Map<String, dynamic>
        ? data['data'] as Map<String, dynamic>
        : null;
  }
}
