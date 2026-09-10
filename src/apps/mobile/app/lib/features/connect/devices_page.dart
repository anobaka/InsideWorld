import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../../core/connection.dart';
import '../../core/models.dart';
import '../../l10n/app_localizations.dart';

/// Who can reach this server, and who is asking to.
///
/// It exists on the phone because the server often has no screen. A container has
/// nobody standing at it to approve a new device, and the fallback — reading a
/// pairing code out of its log — is a thing to ask of somebody once, not every time
/// they get a new phone. Any paired device may approve another, so this page is the
/// approval surface for every headless install.
class DevicesPage extends ConsumerStatefulWidget {
  const DevicesPage({super.key, required this.api});

  final BakabaseApiClient api;

  @override
  ConsumerState<DevicesPage> createState() => _DevicesPageState();
}

class _DevicesPageState extends ConsumerState<DevicesPage> {
  /// Requests expire, and someone at another device may approve one while this page
  /// is open. Neither is worth a push channel; both are worth not lying about.
  static const _pollInterval = Duration(seconds: 5);

  Timer? _timer;
  List<RemoteDevice> _devices = const [];
  List<PendingPairingRequest> _requests = const [];
  bool _loading = true;
  String? _error;
  bool _busy = false;

  @override
  void initState() {
    super.initState();
    _load();
    _timer = Timer.periodic(_pollInterval, (_) => _load(quiet: true));
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  /// [quiet] is the polling case: it must not replace a readable page with an
  /// error because one poll fell over on a flaky network, and it stands aside
  /// while an approval or a revocation is in flight.
  Future<void> _load({bool quiet = false}) async {
    if (quiet && _busy) {
      return;
    }

    try {
      final devices = await widget.api.devices();

      // Asked for separately, and a failure here is not a failure of the page: an
      // older server has no such route, and the list of devices is still worth
      // showing when nothing is waiting to join.
      final requests = await _pendingRequests();

      if (!mounted) {
        return;
      }

      setState(() {
        _devices = devices;
        _requests = requests;
        _loading = false;
        _error = null;
      });
    } on ApiException catch (e) {
      if (!mounted || quiet) {
        return;
      }

      setState(() {
        _loading = false;
        _error = e.message;
      });
    }
  }

  Future<List<PendingPairingRequest>> _pendingRequests() async {
    try {
      return await widget.api.pairingRequests();
    } on ApiException {
      return const [];
    }
  }

  Future<void> _act(Future<void> Function() action) async {
    setState(() => _busy = true);
    try {
      await action();
      await _load();
    } on ApiException catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message)));
      }
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  Future<void> _revoke(RemoteDevice device) async {
    final l10n = AppLocalizations.of(context)!;
    final isSelf = device.id == widget.api.deviceId;

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(l10n.deviceRevokeTitle(device.name)),
        // Revoking the phone in your hand is allowed on purpose — it is how you cut
        // off a device you no longer have — but it ends this session, so it says so.
        content: Text(isSelf ? l10n.deviceRevokeSelfBody : l10n.deviceRevokeBody),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: Text(l10n.cancel),
          ),
          FilledButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: Text(l10n.deviceRevoke),
          ),
        ],
      ),
    );

    if (confirmed != true) {
      return;
    }

    await _act(() async {
      await widget.api.revokeDevice(device.id);

      if (!isSelf || !mounted) {
        return;
      }

      // The key this app holds is now worthless, so it goes too: leaving it in the
      // keystore would only produce refusals nobody could explain. Popping first
      // keeps the page from polling an endpoint that will answer 401 from here on.
      final connection = ref.read(connectionProvider);

      Navigator.of(context).pop();

      if (connection is Connected) {
        await ref.read(connectionProvider.notifier).forget(connection.server.id);
      }
    });
  }

  Future<void> _rename(RemoteDevice device) async {
    final l10n = AppLocalizations.of(context)!;
    final controller = TextEditingController(text: device.name);

    final name = await showDialog<String>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(l10n.deviceRenameTitle),
        content: TextField(
          controller: controller,
          autofocus: true,
          decoration: InputDecoration(labelText: l10n.pairDeviceNameLabel),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(),
            child: Text(l10n.cancel),
          ),
          FilledButton(
            onPressed: () => Navigator.of(context).pop(controller.text.trim()),
            child: Text(l10n.save),
          ),
        ],
      ),
    );

    if (name == null || name.isEmpty || name == device.name) {
      return;
    }

    await _act(() => widget.api.renameDevice(device.id, name));
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context)!;

    return Scaffold(
      appBar: AppBar(title: Text(l10n.devicesTitle)),
      body: RefreshIndicator(
        onRefresh: _load,
        child: _buildBody(l10n),
      ),
    );
  }

  Widget _buildBody(AppLocalizations l10n) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }

    if (_error != null) {
      return ListView(
        children: [
          Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              children: [
                Text(_error!, textAlign: TextAlign.center),
                const SizedBox(height: 12),
                FilledButton(onPressed: _load, child: Text(l10n.retry)),
              ],
            ),
          ),
        ],
      );
    }

    return ListView(
      children: [
        if (_requests.isNotEmpty) ...[
          _SectionHeader(title: l10n.devicesWaiting),
          for (final request in _requests) _requestTile(l10n, request),
          const Divider(height: 24),
        ],
        _SectionHeader(title: l10n.devicesPaired),
        if (_devices.isEmpty)
          ListTile(title: Text(l10n.devicesNone))
        else
          for (final device in _devices) _deviceTile(l10n, device),
      ],
    );
  }

  Widget _requestTile(AppLocalizations l10n, PendingPairingRequest request) => ListTile(
        leading: Icon(_platformIcon(request.platform)),
        title: Text(request.deviceName),
        // The address is the only thing an approver can check the request against:
        // it is what tells "the phone in my hand" from somebody else's.
        subtitle: Text(request.remoteAddress ?? l10n.devicesUnknownAddress),
        trailing: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextButton(
              onPressed: _busy
                  ? null
                  : () => _act(() => widget.api.rejectPairingRequest(request.id)),
              child: Text(l10n.devicesReject),
            ),
            FilledButton(
              onPressed: _busy
                  ? null
                  : () => _act(() => widget.api.approvePairingRequest(request.id)),
              child: Text(l10n.devicesApprove),
            ),
          ],
        ),
      );

  Widget _deviceTile(AppLocalizations l10n, RemoteDevice device) {
    final isSelf = device.id == widget.api.deviceId;

    return ListTile(
      leading: Icon(_platformIcon(device.platform)),
      title: Row(
        children: [
          Flexible(child: Text(device.name, overflow: TextOverflow.ellipsis)),
          if (isSelf) ...[
            const SizedBox(width: 8),
            Chip(
              label: Text(l10n.devicesThisDevice),
              visualDensity: VisualDensity.compact,
              padding: EdgeInsets.zero,
            ),
          ],
        ],
      ),
      subtitle: Text(_lastSeen(l10n, device)),
      trailing: PopupMenuButton<String>(
        enabled: !_busy,
        onSelected: (value) => value == 'rename' ? _rename(device) : _revoke(device),
        itemBuilder: (context) => [
          PopupMenuItem(value: 'rename', child: Text(l10n.deviceRenameTitle)),
          PopupMenuItem(value: 'revoke', child: Text(l10n.deviceRevoke)),
        ],
      ),
    );
  }

  String _lastSeen(AppLocalizations l10n, RemoteDevice device) {
    final seen = device.lastSeenAt;

    if (seen == null) {
      return l10n.devicesNeverSeen;
    }

    final ago = DateTime.now().toUtc().difference(seen);

    if (ago.inMinutes < 1) {
      return l10n.devicesSeenJustNow;
    }
    if (ago.inHours < 1) {
      return l10n.devicesSeenMinutesAgo(ago.inMinutes);
    }
    if (ago.inDays < 1) {
      return l10n.devicesSeenHoursAgo(ago.inHours);
    }

    return l10n.devicesSeenDaysAgo(ago.inDays);
  }

  /// RemoteDevicePlatform: 0 unknown, 1 Windows, 2 macOS, 3 Linux, 4 Android, 5 iOS.
  IconData _platformIcon(int platform) => switch (platform) {
        1 || 2 || 3 => Icons.computer,
        4 || 5 => Icons.smartphone,
        _ => Icons.devices_other,
      };
}

class _SectionHeader extends StatelessWidget {
  const _SectionHeader({required this.title});

  final String title;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 8),
        child: Text(
          title,
          style: Theme.of(context).textTheme.titleSmall?.copyWith(
                color: Theme.of(context).colorScheme.primary,
              ),
        ),
      );
}
