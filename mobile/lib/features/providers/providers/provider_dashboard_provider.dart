import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:geolocator/geolocator.dart';
import 'package:flutter_tts/flutter_tts.dart';
import 'package:latlong2/latlong.dart' as latlong;
import 'package:dio/dio.dart';

import '../services/provider_service.dart';

class ProviderDashboardProvider extends ChangeNotifier {
  final ProviderService providerService;
  final FlutterTts flutterTts = FlutterTts();
  final Dio _dio = Dio();

  ProviderDashboardProvider({required this.providerService});

  bool _isOnline = false;
  double _operatingRadiusKm = 10.0;
  double _latitude = 6.9271; // Default fallback to Colombo, Sri Lanka
  double _longitude = 79.8612;
  bool _isLoading = false;
  String? _error;

  // Stores the real job dispatched from your Python/C# backend
  Map<String, dynamic>? activeJobMatch;
  String? _lastAnnouncedJobId;
  List<latlong.LatLng> routePoints = [];

  bool get isOnline => _isOnline;
  double get operatingRadiusKm => _operatingRadiusKm;
  double get latitude => _latitude;
  double get longitude => _longitude;
  bool get isLoading => _isLoading;
  String? get error => _error;

  StreamSubscription<Position>? _positionStreamSubscription;
  Timer? _pollingTimer;

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
      _startPolling();
    } else {
      _stopPolling();
      _clearJobState();
    }
  }
  
  void _startPolling() {
    _pollingTimer?.cancel();
    _pollingTimer = Timer.periodic(const Duration(seconds: 5), (_) {
      if (_isOnline) {
        fetchLatestDispatchedJob();
      }
    });
  }

  void _stopPolling() {
    _pollingTimer?.cancel();
    _pollingTimer = null;
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
        final data = Map<String, dynamic>.from(response.data);
        activeJobMatch = data;
        
        final jobId = data['jobId']?.toString();
        if (jobId != null && jobId != _lastAnnouncedJobId) {
          _lastAnnouncedJobId = jobId;
          _announceJob(data);
        }

        // Fetch OSRM route if coordinates are available
        final dynamic rawLat = data['customerLatitude'];
        final dynamic rawLng = data['customerLongitude'];
        if (rawLat != null && rawLng != null && routePoints.isEmpty) {
          final double custLat = (rawLat as num).toDouble();
          final double custLng = (rawLng as num).toDouble();
          await _fetchRoute(custLat, custLng);
        }
        
        notifyListeners();
        return;
      } else {
        _clearJobState();
        return;
      }
    } catch (e) {
      debugPrint('Network fetch for active dispatch failed or was empty: $e');
      _clearJobState();
    }
  }

  Future<void> _fetchRoute(double custLat, double custLng) async {
    try {
      final url = 'https://router.project-osrm.org/route/v1/driving/$_longitude,$_latitude;$custLng,$custLat?geometries=geojson';
      final response = await _dio.get(url);
      if (response.statusCode == 200 && response.data != null) {
        final routes = response.data['routes'] as List;
        if (routes.isNotEmpty) {
          final geometry = routes[0]['geometry'];
          final coordinates = geometry['coordinates'] as List;
          routePoints = coordinates.map((coord) {
            return latlong.LatLng(
              (coord[1] as num).toDouble(),
              (coord[0] as num).toDouble(),
            );
          }).toList();
          notifyListeners();
        }
      }
    } catch (e) {
      debugPrint('Failed to fetch OSRM route: $e');
    }
  }

  Future<void> _announceJob(Map<String, dynamic> data) async {
    final category = data['category']?.toString() ?? 'Service Request';
    final distance = data['distanceKm']?.toString() ?? 'unknown';
    final urgency = data['urgency']?.toString() ?? 'Standard';
    await flutterTts.speak('New $category job alert, $distance kilometers away. $urgency urgency.');
  }
  
  void _clearJobState() {
    activeJobMatch = null;
    routePoints = [];
    notifyListeners();
  }

  void clearActiveJobMatch() {
    _clearJobState();
  }
  
  Future<void> declineJob() async {
    try {
      await providerService.declineActiveDispatch();
      _clearJobState();
      _lastAnnouncedJobId = null;
    } catch (e) {
      debugPrint('Failed to decline match: $e');
    }
  }

  void _setLoading(bool value) {
    _isLoading = value;
    notifyListeners();
  }

  @override
  void dispose() {
    _stopPolling();
    _positionStreamSubscription?.cancel();
    super.dispose();
  }
}
