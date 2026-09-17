import 'dart:async';
import 'package:flutter/foundation.dart';
import 'package:geolocator/geolocator.dart';
import '../services/provider_service.dart';

class ProviderDashboardProvider extends ChangeNotifier {
  final ProviderService providerService;

  ProviderDashboardProvider({required this.providerService});

  bool _isOnline = false;
  double _operatingRadiusKm = 10.0;
  double _latitude = 6.9271; // Default to Colombo, Sri Lanka
  double _longitude = 79.8612;
  bool _isLoading = false;
  String? _error;
  
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

      Position? position = await Geolocator.getLastKnownPosition();
      position ??= await Geolocator.getCurrentPosition();
      
      _latitude = position.latitude;
      _longitude = position.longitude;

      _positionStreamSubscription = Geolocator.getPositionStream().listen((Position pos) {
        _latitude = pos.latitude;
        _longitude = pos.longitude;
        notifyListeners();
        if (_isOnline) {
          _syncAvailability();
        }
      });
      
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
