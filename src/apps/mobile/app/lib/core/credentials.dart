import 'dart:convert';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// What a paired device signs with.
///
/// The key is handed out exactly once, by the endpoint that completes pairing, and
/// never again — so losing it means pairing again, and leaking it means someone else
/// can act as this device until it is revoked.
class DeviceCredentials {
  const DeviceCredentials({required this.deviceId, required this.key});

  final String deviceId;

  /// base64url of the 32-byte HMAC key, as the server issued it.
  final String key;

  Map<String, dynamic> toJson() => {'deviceId': deviceId, 'key': key};

  static DeviceCredentials? fromJson(Map<String, dynamic> json) {
    final deviceId = json['deviceId'] as String?;
    final key = json['key'] as String?;

    if (deviceId == null || deviceId.isEmpty || key == null || key.isEmpty) {
      return null;
    }

    return DeviceCredentials(deviceId: deviceId, key: key);
  }
}

/// Where credentials live: the platform keystore, not shared preferences.
///
/// Server profiles are ordinary preferences — an address and a name are not secrets —
/// but the key is bearer authority over someone's library for as long as the device
/// stays paired. On Android that means EncryptedSharedPreferences, on iOS the Keychain;
/// both survive backup/restore differently from preferences, which is a feature here:
/// a key restored onto a different phone is a device the server never paired with.
class CredentialStore {
  CredentialStore([FlutterSecureStorage? storage])
      : _storage = storage ??
            const FlutterSecureStorage(
              aOptions: AndroidOptions(encryptedSharedPreferences: true),
              iOptions: IOSOptions(accessibility: KeychainAccessibility.first_unlock),
            );

  final FlutterSecureStorage _storage;

  /// One entry per server: a phone can be paired with several, each with its own key.
  static String keyFor(String serverId) => 'bakabase.credentials.$serverId';

  Future<DeviceCredentials?> read(String serverId) async {
    final raw = await _storage.read(key: keyFor(serverId));

    if (raw == null || raw.isEmpty) {
      return null;
    }

    try {
      final json = jsonDecode(raw);

      return json is Map<String, dynamic> ? DeviceCredentials.fromJson(json) : null;
    } on FormatException {
      return null;
    }
  }

  Future<void> write(String serverId, DeviceCredentials credentials) =>
      _storage.write(key: keyFor(serverId), value: jsonEncode(credentials.toJson()));

  /// Forgetting a server has to forget its key too, or a later re-pair would leave
  /// the old one behind in the keystore for nobody.
  Future<void> delete(String serverId) => _storage.delete(key: keyFor(serverId));
}
