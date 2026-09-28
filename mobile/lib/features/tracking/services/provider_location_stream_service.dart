import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:geolocator/geolocator.dart';

import 'tracking_signalr_service.dart';

class ProviderLocationStreamService {
  final TrackingSignalRService _signalRService;
  StreamSubscription<Position>? _positionSubscription;
  String? _jobId;
  bool _isOnTheWay = false;
  bool _isSending = false;

  ProviderLocationStreamService({TrackingSignalRService? signalRService})
      : _signalRService = signalRService ?? TrackingSignalRService();

  Future<void> start({
    required String jobId,
    required bool isOnTheWay,
    TrackingStoppedCallback? onStopped,
  }) async {
    if (!isOnTheWay) return;

    await stop();
    await _ensureLocationPermission();
    _jobId = jobId;
    _isOnTheWay = true;

    await _signalRService.connect(
      jobId: jobId,
      onStopped: () async {
        await stop();
        onStopped?.call();
      },
    );

    final locationSettings = _locationSettings();
    _positionSubscription = Geolocator.getPositionStream(
      locationSettings: locationSettings,
    ).listen(_sendPosition);
  }

  Future<void> _sendPosition(Position position) async {
    final jobId = _jobId;
    if (!_isOnTheWay || jobId == null || _isSending) return;

    _isSending = true;
    try {
      await _signalRService.updateLocation(
        jobId: jobId,
        latitude: position.latitude,
        longitude: position.longitude,
      );
    } finally {
      _isSending = false;
    }
  }

  Future<void> stop() async {
    _isOnTheWay = false;
    _jobId = null;
    await _positionSubscription?.cancel();
    _positionSubscription = null;
    await _signalRService.stop();
  }

  Future<void> _ensureLocationPermission() async {
    if (!await Geolocator.isLocationServiceEnabled()) {
      throw StateError('Location services are disabled.');
    }

    var permission = await Geolocator.checkPermission();
    if (permission == LocationPermission.denied) {
      permission = await Geolocator.requestPermission();
    }

    if (permission == LocationPermission.denied ||
        permission == LocationPermission.deniedForever) {
      throw StateError('Location permission is required for live tracking.');
    }
  }

  LocationSettings _locationSettings() {
    if (defaultTargetPlatform == TargetPlatform.android) {
      return AndroidSettings(
        accuracy: LocationAccuracy.high,
        distanceFilter: 0,
        intervalDuration: const Duration(seconds: 5),
        foregroundNotificationConfig: const ForegroundNotificationConfig(
          notificationTitle: 'AssistLK live tracking',
          notificationText: 'Live provider location is being shared.',
          enableWakeLock: true,
        ),
      );
    }

    if (defaultTargetPlatform == TargetPlatform.iOS) {
      return AppleSettings(
        accuracy: LocationAccuracy.high,
        activityType: ActivityType.automotiveNavigation,
        distanceFilter: 0,
        pauseLocationUpdatesAutomatically: false,
        showBackgroundLocationIndicator: true,
      );
    }

    return const LocationSettings(
      accuracy: LocationAccuracy.high,
      distanceFilter: 0,
    );
  }
}