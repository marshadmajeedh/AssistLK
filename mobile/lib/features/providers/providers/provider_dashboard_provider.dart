import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:geolocator/geolocator.dart';
import 'package:flutter_tts/flutter_tts.dart';
import 'package:latlong2/latlong.dart' as latlong;
import 'package:dio/dio.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../services/provider_service.dart';

class ProviderDashboardProvider extends ChangeNotifier {
  static const String _prefIsOnlineKey = 'provider_is_online';
  static const String _prefOperatingRadiusKey = 'provider_operating_radius_km';

  final ProviderService providerService;
  final FlutterTts flutterTts = FlutterTts();
  final Dio _dio = Dio();

  ProviderDashboardProvider({required this.providerService});

  Map<String, dynamic>? profile;
  bool _isProfileLoading = true;
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

  bool get isProfileLoading => _isProfileLoading;
  bool get isOnline => _isOnline;
  double get operatingRadiusKm => _operatingRadiusKm;
  double get latitude => _latitude;
  double get longitude => _longitude;
  bool get isLoading => _isLoading;
  String? get error => _error;

  String get verificationStatus =>
      profile?['verificationStatus']?.toString() ?? 'Pending';
  bool get isVerified => verificationStatus.toLowerCase() == 'verified';
  bool get isPending => verificationStatus.toLowerCase() == 'pending';
  bool get isRejected => verificationStatus.toLowerCase() == 'rejected';

  String get fullName => profile?['fullName']?.toString() ?? '';
  String get businessName => profile?['businessName']?.toString() ?? '';
  String get category => profile?['category']?.toString() ?? 'Plumbing';
  double get rating => (profile?['rating'] as num?)?.toDouble() ?? 5.0;

  StreamSubscription<Position>? _positionStreamSubscription;
  Timer? _pollingTimer;

  Future<void> init() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      _isOnline = prefs.getBool(_prefIsOnlineKey) ?? false;
      _operatingRadiusKm = prefs.getDouble(_prefOperatingRadiusKey) ?? 10.0;
      notifyListeners();
    } catch (e) {
      debugPrint('Failed to load persisted provider preferences: $e');
    }

    await fetchProfile();
  }

  Future<void> fetchProfile() async {
    _isProfileLoading = true;
    _error = null;
    notifyListeners();

    try {
      final data = await providerService.getProfile();
      profile = data;

      final num? rad = data['operatingRadiusKm'] as num?;
      if (rad != null && rad > 0) {
        _operatingRadiusKm = rad.toDouble();
      }
      final num? lat = data['latitude'] as num?;
      final num? lng = data['longitude'] as num?;
      if (lat != null && lng != null && lat != 0 && lng != 0) {
        _latitude = lat.toDouble();
        _longitude = lng.toDouble();
      }
      if (data['isOnline'] is bool) {
        _isOnline = data['isOnline'] as bool;
      }

      _isProfileLoading = false;
      notifyListeners();

      if (isVerified) {
        unawaited(_checkPermissionsAndFetchLocation());
        if (_isOnline) {
          fetchLatestDispatchedJob();
          _startPolling();
        }
      } else {
        _stopPolling();
        _clearJobState();
      }
    } catch (e) {
      debugPrint('Failed to load provider profile: $e');
      _error = 'Failed to load profile: $e';
      _isProfileLoading = false;
      notifyListeners();
    }
  }

  Future<void> updateProfile({
    required String businessName,
    required double operatingRadiusKm,
    double? latitude,
    double? longitude,
  }) async {
    _isLoading = true;
    notifyListeners();
    try {
      final updated = await providerService.updateProfile(
        businessName: businessName,
        operatingRadiusKm: operatingRadiusKm,
        latitude: latitude,
        longitude: longitude,
      );
      profile = {...?profile, ...updated};
      _operatingRadiusKm = operatingRadiusKm;
      if (latitude != null) _latitude = latitude;
      if (longitude != null) _longitude = longitude;
      _isLoading = false;
      notifyListeners();
      await _saveOperatingRadius(operatingRadiusKm);
    } catch (e) {
      _isLoading = false;
      _error = 'Failed to update profile: $e';
      notifyListeners();
      rethrow;
    }
  }

  Future<void> _checkPermissionsAndFetchLocation() async {
    _error = null;
    try {
      bool serviceEnabled = await Geolocator.isLocationServiceEnabled();
      if (!serviceEnabled) {
        _error = 'Location services are disabled.';
        notifyListeners();
        return;
      }

      LocationPermission permission = await Geolocator.checkPermission();
      if (permission == LocationPermission.denied) {
        permission = await Geolocator.requestPermission();
        if (permission == LocationPermission.denied) {
          _error = 'Location permissions are denied.';
          notifyListeners();
          return;
        }
      }

      if (permission == LocationPermission.deniedForever) {
        _error = 'Location permissions are permanently denied.';
        notifyListeners();
        return;
      }

      // Safe location acquisition
      Position position;
      if (kIsWeb) {
        position = await Geolocator.getCurrentPosition(
          locationSettings: const LocationSettings(
            accuracy: LocationAccuracy.high,
          ),
        );
      } else {
        Position? lastKnown = await Geolocator.getLastKnownPosition();
        position =
            lastKnown ??
            await Geolocator.getCurrentPosition(
              locationSettings: const LocationSettings(
                accuracy: LocationAccuracy.high,
              ),
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
            if (_isOnline && isVerified) {
              _syncAvailability();
            }
          });

      _error = null;
      notifyListeners();

      if (_isOnline && isVerified) {
        _syncAvailability();
      }
    } catch (e) {
      _error = 'Error fetching location: $e';
      notifyListeners();
    }
  }

  void toggleOnlineStatus(bool value) {
    if (!isVerified) return;

    _isOnline = value;
    notifyListeners();
    _saveIsOnline(value);
    _syncAvailability();

    if (_isOnline) {
      fetchLatestDispatchedJob();
      _startPolling();
    } else {
      _stopPolling();
      _clearJobState();
    }
  }

  Future<void> _saveIsOnline(bool value) async {
    try {
      final prefs = await SharedPreferences.getInstance();
      await prefs.setBool(_prefIsOnlineKey, value);
    } catch (e) {
      debugPrint('Failed to save isOnline preference: $e');
    }
  }

  void _startPolling() {
    if (!isVerified) return;
    _pollingTimer?.cancel();
    _pollingTimer = Timer.periodic(const Duration(seconds: 5), (_) {
      if (_isOnline && isVerified) {
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
    _saveOperatingRadius(value);
  }

  void onRadiusChangeEnd(double value) {
    _saveOperatingRadius(value);
    if (isVerified) {
      _syncAvailability();
    }
  }

  Future<void> _saveOperatingRadius(double value) async {
    try {
      final prefs = await SharedPreferences.getInstance();
      await prefs.setDouble(_prefOperatingRadiusKey, value);
    } catch (e) {
      debugPrint('Failed to save operatingRadius preference: $e');
    }
  }

  Future<void> _syncAvailability() async {
    if (!isVerified) return;
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
    if (!isVerified) return;
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
      final url =
          'https://router.project-osrm.org/route/v1/driving/$_longitude,$_latitude;$custLng,$custLat?geometries=geojson';
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
    await flutterTts.speak(
      'New $category job alert, $distance kilometers away. $urgency urgency.',
    );
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

  @override
  void dispose() {
    _stopPolling();
    _positionStreamSubscription?.cancel();
    super.dispose();
  }
}
