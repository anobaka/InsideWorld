import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'api_client.dart';
import 'credentials.dart';
import 'models.dart';
import 'server_profiles.dart';

/// Protocol versions this build of the app can talk to. Compared against the
/// server's protocolVersion during the handshake.
const int minSupportedProtocol = 1;
const int maxSupportedProtocol = 1;

sealed class ServerConnectionState {
  const ServerConnectionState();
}

class Disconnected extends ServerConnectionState {
  const Disconnected();
}

class Connecting extends ServerConnectionState {
  const Connecting(this.baseUrl);

  final String baseUrl;
}

class Connected extends ServerConnectionState {
  const Connected(this.api, this.server);

  final BakabaseApiClient api;
  final ServerInfo server;
}

/// Reached and understood, but this device has no key and the server insists on one.
///
/// A state rather than a failure: nothing is wrong, the user simply has to pair. It
/// carries what the pairing screen needs so that screen does not have to handshake
/// again.
class NeedsPairing extends ServerConnectionState {
  const NeedsPairing(this.baseUrl, this.server, this.clockOffset);

  final String baseUrl;
  final ServerInfo server;

  /// Measured during the handshake that discovered the need to pair, so the very
  /// first signed request is already on the server's clock.
  final Duration clockOffset;
}

/// Why a connect attempt failed — as data, so the UI layer owns the wording
/// (and its translation).
enum ConnectionFailureKind { network, protocolTooNew, protocolTooOld }

class ConnectionFailed extends ServerConnectionState {
  const ConnectionFailed(this.baseUrl, this.kind, this.detail, {this.denial});

  final String baseUrl;
  final ConnectionFailureKind kind;

  /// The raw error message for [ConnectionFailureKind.network]; the server's
  /// protocol version for the protocol kinds.
  final String detail;
  final RemoteAccessDenial? denial;
}

class ConnectionController extends Notifier<ServerConnectionState> {
  final ServerProfileStore _profiles = ServerProfileStore();
  final CredentialStore _credentials = CredentialStore();

  @override
  ServerConnectionState build() => const Disconnected();

  /// The whole connect handshake: reach the server, learn who it is, check
  /// protocol compatibility, remember it on success.
  Future<void> connect(String baseUrl) async {
    state = Connecting(baseUrl);

    // The handshake is unsigned: server-info is reachable without a key, which is
    // what lets a device learn who it is talking to before deciding whether to pair.
    final sentAt = DateTime.now().toUtc();
    final ServerInfo info;
    try {
      info = await BakabaseApiClient(baseUrl).serverInfo();
    } on ApiException catch (e) {
      state = ConnectionFailed(baseUrl, ConnectionFailureKind.network, e.message,
          denial: e.denial);
      return;
    }

    if (info.protocolVersion > maxSupportedProtocol) {
      state = ConnectionFailed(
          baseUrl, ConnectionFailureKind.protocolTooNew, '${info.protocolVersion}');
      return;
    }
    if (info.protocolVersion < minSupportedProtocol) {
      state = ConnectionFailed(
          baseUrl, ConnectionFailureKind.protocolTooOld, '${info.protocolVersion}');
      return;
    }

    final offset = _measureOffset(info.serverTime, sentAt);
    final credentials = await _credentials.read(info.id);

    await _profiles.save(ServerProfile(
      id: info.id,
      name: info.name,
      baseUrl: baseUrl,
      lastConnectedAt: DateTime.now(),
      paired: credentials != null,
    ));

    final api = BakabaseApiClient(baseUrl, credentials: credentials, clockOffset: offset);

    // Unpaired is fine on a server that does not require pairing — most of them, since
    // the switch is off by default. So rather than guessing from flags, ask for
    // something real and let the gate answer; only an Unauthenticated refusal means
    // this device actually has to pair.
    if (credentials == null && info.pairingSupported) {
      try {
        await api.mediaLibraries();
      } on ApiException catch (e) {
        if (e.denial == RemoteAccessDenial.unauthenticated) {
          state = NeedsPairing(baseUrl, info, offset);
          return;
        }
      }
    }

    state = Connected(api, info);
  }

  /// Stores the credentials a pairing produced and connects with them.
  Future<void> completePairing(
      String baseUrl, ServerInfo server, Duration clockOffset, DeviceCredentials credentials) async {
    await _credentials.write(server.id, credentials);
    await _profiles.setPaired(server.id, true);

    state = Connected(
      BakabaseApiClient(baseUrl, credentials: credentials, clockOffset: clockOffset),
      server,
    );
  }

  /// Forgets a server, and its key with it — leaving the key behind would strand a
  /// secret in the keystore for a server the user meant to be rid of.
  Future<void> forget(String serverId) async {
    await _credentials.delete(serverId);
    await _profiles.remove(serverId);

    if (state case Connected(server: final s) when s.id == serverId) {
      state = const Disconnected();
    }
  }

  /// How far this device's clock sits from the server's.
  ///
  /// Half the round trip is charged to the outbound leg, which is the usual
  /// approximation and is far below the five minutes of skew the server allows. Zero
  /// when the server did not report a time — an older one — which leaves this device
  /// signing with its own clock, exactly as it would have before.
  static Duration _measureOffset(DateTime? serverTime, DateTime sentAt) {
    if (serverTime == null) {
      return Duration.zero;
    }

    final roundTrip = DateTime.now().toUtc().difference(sentAt);

    if (roundTrip.isNegative) {
      return Duration.zero;
    }

    return serverTime.difference(sentAt.add(roundTrip ~/ 2));
  }

  void disconnect() {
    state = const Disconnected();
  }
}

final connectionProvider = NotifierProvider<ConnectionController, ServerConnectionState>(
  ConnectionController.new,
);

final serverProfilesProvider = FutureProvider<List<ServerProfile>>((ref) {
  // Re-reads whenever the connection changes, so a fresh connect reorders the
  // remembered list.
  ref.watch(connectionProvider);
  return ServerProfileStore().load();
});
