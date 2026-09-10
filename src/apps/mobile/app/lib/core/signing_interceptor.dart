import 'dart:convert';

import 'package:dio/dio.dart';

import 'credentials.dart';
import 'request_signature.dart';

/// Signs every outgoing request with the device key.
///
/// The canonical string it builds has to match the one the server rebuilds from the
/// request it received, byte for byte, so everything here is about not disturbing what
/// Dio is about to put on the wire: the query is read back from the composed URI rather
/// than re-encoded from the parameter map, and the body is hashed from the exact bytes
/// that will be sent.
class DeviceSigningInterceptor extends Interceptor {
  DeviceSigningInterceptor({
    required DeviceCredentials Function() credentials,
    required Duration Function() clockOffset,
  })  : _credentials = credentials,
        _clockOffset = clockOffset;

  final DeviceCredentials Function() _credentials;

  /// How far this device's clock sits from the server's, measured at handshake. The
  /// server allows five minutes of skew, which absorbs drift but not a phone in the
  /// wrong timezone or with a badly set clock — and a phone is far more likely to have
  /// one than a desktop.
  final Duration Function() _clockOffset;

  @override
  void onRequest(RequestOptions options, RequestInterceptorHandler handler) {
    final credentials = _credentials();

    if (credentials.deviceId.isEmpty) {
      handler.next(options);
      return;
    }

    final uri = options.uri;
    final timestamp =
        (DateTime.now().toUtc().add(_clockOffset()).millisecondsSinceEpoch / 1000).floor();
    final nonce = RemoteRequestSignature.newNonce();

    final canonical = RemoteRequestSignature.buildCanonicalString(
      deviceId: credentials.deviceId,
      method: options.method,
      path: uri.path,
      // The query exactly as it will travel. Rebuilding it from queryParameters would
      // re-encode it, and a path in `?fullname=` comes out differently every time.
      rawQuery: uri.query,
      timestampSeconds: timestamp,
      nonce: nonce,
      bodyDigest: bodyDigest(options),
    );

    options.headers['Authorization'] = RemoteRequestSignature.buildHeader(
      credentials.deviceId,
      timestamp,
      nonce,
      RemoteRequestSignature.sign(
        RemoteRequestSignature.fromBase64Url(credentials.key),
        canonical,
      ),
    );

    handler.next(options);
  }

  /// Whether this request's body is part of what gets signed.
  ///
  /// Mirrors the server's rule exactly, and the desktop client's: GET and HEAD never
  /// hash, and neither does a body of unknown length or one past the limit — buffering
  /// an upload twice to hash it costs more than the replay protection is worth. The
  /// method, path, query, timestamp and nonce are covered either way.
  ///
  /// Note that a zero-length body is *not* hashed, which is not the same as hashing
  /// nothing: the server keys off ContentLength > 0, so an empty POST carries the empty
  /// digest line. Getting that backwards fails every empty POST and nothing else, which
  /// is exactly the kind of thing that gets found in production.
  static bool shouldHashBody(String method, int? contentLength) =>
      method.toUpperCase() != 'GET' &&
      method.toUpperCase() != 'HEAD' &&
      contentLength != null &&
      contentLength > 0 &&
      contentLength <= RemoteRequestSignature.maxHashedBodyBytes;

  /// The digest line for this request: base64url of SHA-256, or the empty string.
  static String bodyDigest(RequestOptions options) {
    final bytes = _bodyBytes(options);

    return shouldHashBody(options.method, bytes?.length)
        ? RemoteRequestSignature.hashBody(bytes!)
        : '';
  }

  /// The bytes the body will be sent as, or null when Dio will not send a
  /// length-known body at all.
  static List<int>? _bodyBytes(RequestOptions options) {
    final data = options.data;

    if (data == null) {
      return null;
    }

    if (data is List<int>) {
      return data;
    }

    if (data is String) {
      return utf8.encode(data);
    }

    if (data is Map || data is List) {
      // What Dio's JSON transformer will encode it to. Same encoder, same bytes.
      return utf8.encode(jsonEncode(data));
    }

    // A stream or form data: Dio composes it later and its length is not known here,
    // so it is signed as though there were no body — which is what the server does with
    // a request whose ContentLength it cannot read either.
    return null;
  }
}
