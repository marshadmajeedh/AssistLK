import 'dart:async';

import 'package:flutter/foundation.dart';

import '../../service_requests/models/location_source.dart';
import '../../service_requests/services/location_service.dart';
import '../../service_requests/services/location_geocoding_service.dart';
import '../models/location_suggestion.dart';

enum CustomerLocationState {
  idle,
  capturing,
  resolving,
  resolved,
  denied,
  deniedForever,
  servicesDisabled,
  captureFailed,
  geocodingFailed,
}

class CustomerLocationProvider extends ChangeNotifier {
  final LocationService gps;
  final LocationGeocodingService geocoding;
  final DateTime Function() clock;
  CustomerLocationProvider({
    required this.gps,
    required this.geocoding,
    DateTime Function()? clock,
  }) : clock = clock ?? DateTime.now;

  CustomerLocationState _state = CustomerLocationState.idle;
  LocationSuggestion? _suggestion;
  Timer? _expiry;
  int _generation = 0;
  bool _disposed = false;

  CustomerLocationState get state => _state;
  LocationSuggestion? get suggestion => _suggestion;
  bool get busy =>
      state == CustomerLocationState.capturing ||
      state == CustomerLocationState.resolving;
  LocationSuggestion? get freshSuggestion =>
      state == CustomerLocationState.resolved &&
          (_suggestion?.isFresh(clock()) ?? false)
      ? _suggestion
      : null;

  /// Only called by explicit user actions; no startup permission checks or GPS.
  Future<void> capture() async {
    if (_disposed || busy) return;
    final generation = ++_generation;
    bool current() => !_disposed && generation == _generation;
    _expiry?.cancel();
    _state = CustomerLocationState.capturing;
    notifyListeners();
    try {
      final result = await gps.getCurrentLocation();
      if (!current()) return;
      if (!result.isSuccess) {
        _state = switch (result.status) {
          LocationAccessStatus.denied => CustomerLocationState.denied,
          LocationAccessStatus.deniedForever =>
            CustomerLocationState.deniedForever,
          LocationAccessStatus.servicesDisabled =>
            CustomerLocationState.servicesDisabled,
          _ => CustomerLocationState.captureFailed,
        };
        notifyListeners();
        return;
      }
      final coordinates = result.coordinates!;
      final capturedAt = clock();
      _state = CustomerLocationState.resolving;
      notifyListeners();
      final address = await geocoding.reverseGeocode(
        coordinates.latitude,
        coordinates.longitude,
      );
      if (!current()) return;
      _suggestion = LocationSuggestion(
        location: address,
        latitude: coordinates.latitude,
        longitude: coordinates.longitude,
        accuracyMeters: coordinates.accuracy,
        source: LocationSource.openStreetMap,
        capturedAt: capturedAt,
      );
      _state = CustomerLocationState.resolved;
      refreshFreshness();
    } catch (_) {
      if (!current()) return;
      _state = _state == CustomerLocationState.resolving
          ? CustomerLocationState.geocodingFailed
          : CustomerLocationState.captureFailed;
      notifyListeners();
    }
  }

  /// Refreshes the label on expiry/resume, never the device location itself.
  void refreshFreshness() {
    if (_disposed) return;
    _expiry?.cancel();
    final captured = _suggestion;
    if (captured != null && captured.isFresh(clock())) {
      _expiry = Timer(
        captured.capturedAt.add(LocationSuggestion.maxAge).difference(clock()),
        refreshFreshness,
      );
    }
    notifyListeners();
  }

  void reset() {
    _generation++;
    _expiry?.cancel();
    _suggestion = null;
    _state = CustomerLocationState.idle;
    if (!_disposed) notifyListeners();
  }

  @override
  void dispose() {
    _disposed = true;
    _generation++;
    _expiry?.cancel();
    _suggestion = null;
    super.dispose();
  }
}
