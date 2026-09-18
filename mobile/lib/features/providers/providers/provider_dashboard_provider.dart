import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:geolocator/geolocator.dart';

import '../services/provider_service.dart';

class ProviderDashboardProvider extends ChangeNotifier {
  final ProviderService providerService;

  ProviderDashboardProvider({required this.providerService});

  bool _isOnline = false;
  double _operatingRadiusKm = 10.0;
  double _latitude = 6.9271; // Default fallback to Colombo, Sri Lanka
  double _longitude = 79.8612;
  bool _isLoading = false;
  String? _error;

  // Stores the real job dispatched from your Python/C# backend
  Map<String, dynamic>? activeJobMatch;

  bool get isOnline => _isOnline;
  double get operatingRadiusKm => _operatingRadiusKm;
  double get latitude => _latitude;
  double get longitude => _longitude;
  bool get isLoading => _isLoading;
  String? get error => _error;

  StreamSubscription<Position>? _positionStreamSubscription;

  Future<void> init() async {
    await _checkPermissionsAndFetchLocation();
  }

  Future<void> _checkPermissionsAndFetchLocation() async {
    _setLoading(true);
    _error = null;
    try {
      bool serviceEnabled = await Geolocator.isLocationServiceEnabled();
      if (!serviceEnabled) {
        _error = 'Location services are disabled.';
        _setLoading(false);
        return;
      }

      LocationPermission permission = await Geolocator.checkPermission();
      if (permission == LocationPermission.denied) {
        permission = await Geolocator.requestPermission();
        if (permission == LocationPermission.denied) {
          _error = 'Location permissions are denied.';
          _setLoading(false);
          return;
        }
      }

      if (permission == LocationPermission.deniedForever) {
        _error = 'Location permissions are permanently denied.';
        _setLoading(false);
        return;
      }

      // Web-safe location acquisition:
      // Chrome/Web throws an UnsupportedOperationException on getLastKnownPosition.
      Position position;
      if (kIsWeb) {
        position = await Geolocator.getCurrentPosition(
          desiredAccuracy: LocationAccuracy.high,
        );
      } else {
        Position? lastKnown = await Geolocator.getLastKnownPosition();
        position =
            lastKnown ??
            await Geolocator.getCurrentPosition(
              desiredAccuracy: LocationAccuracy.high,
            );
      }

      _latitude = position.latitude;
      _longitude = position.longitude;

      // Listen for dynamic positional movement
      _positionStreamSubscription?.cancel();
      _positionStreamSubscription =
          Geolocator.getPositionStream(
            locationSettings: const LocationSettings(
              accuracy: LocationAccuracy.high,
              distanceFilter: 50,
            ),
          ).listen((Position pos) {
            _latitude = pos.latitude;
            _longitude = pos.longitude;
            notifyListeners();
            if (_isOnline) {
              _syncAvailability();
            }
          });

      // Clear any prior error state
      _error = null;

      if (_isOnline) {
        _syncAvailability();
      }
    } catch (e) {
      _error = 'Error fetching location: $e';
    } finally {
      _setLoading(false);
    }
  }

  void toggleOnlineStatus(bool value) {
    _isOnline = value;
    notifyListeners();
    _syncAvailability();

    // Query for active dispatches when turning online
    if (_isOnline) {
      fetchLatestDispatchedJob();
    } else {
      activeJobMatch = null;
      notifyListeners();
    }
  }

  void setOperatingRadius(double value) {
    _operatingRadiusKm = value;
    notifyListeners();
  }

  void onRadiusChangeEnd(double value) {
    _syncAvailability();
  }

  Future<void> _syncAvailability() async {
    try {
      await providerService.updateAvailability(
        isOnline: _isOnline,
        latitude: _latitude,
        longitude: _longitude,
        operatingRadiusKm: _operatingRadiusKm,
      );
    } catch (e) {
      debugPrint('Failed to sync availability: $e');
    }
  }

  // Fetches approved matches from PostgreSQL via ASP.NET Core
  Future<void> fetchLatestDispatchedJob() async {
    try {
      final response = await providerService.apiClient.client.get(
        '/providers/active-dispatch',
      );
      if (response.statusCode == 200 && response.data != null) {
        activeJobMatch = Map<String, dynamic>.from(response.data);
        notifyListeners();
        return;
      } else {
        activeJobMatch = null;
        notifyListeners();
        return;
      }
    } catch (e) {
      debugPrint('Network fetch for active dispatch failed or was empty: $e');
      activeJobMatch = null;
    }

    notifyListeners();
  }

  void clearActiveJobMatch() {
    activeJobMatch = null;
    notifyListeners();
  }

  void _setLoading(bool value) {
    _isLoading = value;
    notifyListeners();
  }

  @override
  void dispose() {
    _positionStreamSubscription?.cancel();
    super.dispose();
  }
}
