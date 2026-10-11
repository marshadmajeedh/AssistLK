import 'package:signalr_netcore/signalr_client.dart';

import '../../../core/auth/token_storage.dart';
import '../../../core/config/app_config.dart';
import '../models/tracking_coordinate.dart';

typedef TrackingLocationCallback = void Function(TrackingCoordinate location);
typedef TrackingStoppedCallback = void Function();

class TrackingSignalRService {
  final TokenStorage _tokenStorage;
  HubConnection? _connection;
  String? _connectedJobId;

  TrackingSignalRService({TokenStorage? tokenStorage})
      : _tokenStorage = tokenStorage ?? TokenStorage();

  Future<void> connect({
    required String jobId,
    TrackingLocationCallback? onLocation,
    TrackingStoppedCallback? onStopped,
  }) async {
    if (_connectedJobId == jobId && _connection != null) return;

    await stop();
    final token = await _tokenStorage.getToken();
    if (token == null || token.isEmpty) {
      throw StateError('Cannot start tracking without an authenticated session.');
    }

    final options = HttpConnectionOptions(
      accessTokenFactory: () async =>
          await _tokenStorage.getToken() ?? token,
    );
    final connection = HubConnectionBuilder()
        .withUrl(AppConfig.trackingHubUrl, options: options)
        .withAutomaticReconnect()
        .build();

    connection.on('ReceiveLocationUpdate', (arguments) {
      if (onLocation == null || arguments == null || arguments.isEmpty) return;
      try {
        onLocation(TrackingCoordinate.fromSignalR(arguments.first));
      } on FormatException {
        // Ignore malformed server data without terminating the connection.
      }
    });

    connection.on('TrackingStopped', (_) => onStopped?.call());
    await connection.start();
    await connection.invoke(
      'JoinJobTrackingGroup',
      args: <Object>[jobId],
    );

    _connection = connection;
    _connectedJobId = jobId;
  }

  Future<void> updateLocation({
    required String jobId,
    required double latitude,
    required double longitude,
  }) async {
    final connection = _connection;
    if (connection == null || _connectedJobId != jobId) {
      throw StateError('Tracking connection is not active for this job.');
    }

    await connection.invoke(
      'UpdateLocation',
      args: <Object>[jobId, latitude, longitude],
    );
  }

  Future<void> stop() async {
    final connection = _connection;
    _connection = null;
    _connectedJobId = null;
    if (connection != null) await connection.stop();
  }
}