import 'package:flutter/material.dart';

import '../models/location_source.dart';
import '../models/resolved_location.dart';
import '../services/location_service.dart';
import '../services/location_geocoding_service.dart';

/// A reviewed address and device coordinates form one selection. No persistence.
class LocationSelectionController extends ChangeNotifier {
  final LocationService gps;
  final LocationGeocodingService geocoding;
  final TextEditingController text;
  double? latitude, longitude, accuracy;
  LocationSource source;
  ResolvedLocation? preview;
  bool busy = false;
  bool needsGpsChoice = false;
  String? message;
  int _generation = 0;
  bool _disposed = false;
  bool _settingText = false;
  late String _lastText;
  String? _resolvedAddress;

  LocationSelectionController({
    required this.gps,
    required this.geocoding,
    String initialText = '',
    this.latitude,
    this.longitude,
    this.source = LocationSource.manual,
  }) : text = TextEditingController(text: initialText) {
    _lastText = initialText;
    if (source == LocationSource.openStreetMap) _resolvedAddress = initialText;
    text.addListener(_edited);
  }

  bool get hasGps => latitude != null && longitude != null;
  bool get canSubmit => !busy && preview == null && !needsGpsChoice;

  void _edited() {
    if (_settingText || text.text == _lastText) return;
    _lastText = text.text;
    // Every edit invalidates both in-flight work and the last keep-GPS decision.
    _generation++;
    busy = false;
    preview = null;
    // Editing a Provider-derived address does not erase its provenance. A fresh
    // manual entry (or explicit GPS removal) is a separate choice below.
    if (text.text.isEmpty) source = LocationSource.manual;
    needsGpsChoice = hasGps;
    message = null;
    notifyListeners();
  }

  void _setText(String value) {
    _settingText = true;
    text.text = value;
    _lastText = value;
    _settingText = false;
  }

  Future<void> capture() async {
    final generation = ++_generation;
    busy = true;
    preview = null;
    message = null;
    notifyListeners();
    bool current() => !_disposed && generation == _generation;
    try {
      final result = await gps.getCurrentLocation();
      if (!current()) return;
      if (!result.isSuccess) {
        message =
            result.message ??
            'Could not retrieve location. Please enter the location manually.';
        return;
      }
      latitude = result.coordinates!.latitude;
      longitude = result.coordinates!.longitude;
      accuracy = result.coordinates!.accuracy;
      // A refreshed point must never silently accompany the old address.
      needsGpsChoice = true;
      final resolved = await geocoding.reverseGeocode(latitude!, longitude!);
      if (!current()) return;
      preview = resolved;
      message = resolved.formattedAddress.length > 255
          ? 'This address exceeds 255 characters. Enter a shorter location manually.'
          : null;
    } catch (_) {
      if (!current()) return;
      message = 'Could not resolve your location. Please enter the location manually or retry.';
    } finally {
      if (current()) {
        busy = false;
        notifyListeners();
      }
    }
  }

  void confirm() {
    final address = preview?.formattedAddress;
    if (busy || address == null || address.length > 255) return;
    _setText(address);
    source = LocationSource.openStreetMap;
    _resolvedAddress = address;
    preview = null;
    needsGpsChoice = false;
    message = 'Location confirmed';
    notifyListeners();
  }

  void enterManually() {
    _generation++;
    busy = false;
    preview = null;
    // Do not relabel a copied resolved address as independently entered content.
    if (source == LocationSource.openStreetMap) _setText('');
    source = LocationSource.manual;
    needsGpsChoice = hasGps;
    message = null;
    notifyListeners();
  }

  void keepGps() {
    if (busy || preview != null) return;
    needsGpsChoice = false;
    message = 'Captured GPS coordinates retained';
    notifyListeners();
  }

  void removeGps() {
    _generation++;
    busy = false;
    preview = null;
    latitude = longitude = accuracy = null;
    if (source == LocationSource.openStreetMap && text.text == _resolvedAddress) {
      _setText('');
    }
    source = LocationSource.manual;
    needsGpsChoice = false;
    message = 'GPS coordinates removed';
    notifyListeners();
  }

  void cancelPending() {
    _generation++;
    busy = false;
    preview = null;
  }

  String? validate() {
    if (text.text.trim().isEmpty) return 'Please provide the service location.';
    if (text.text.trim().length > 255) {
      return 'Location cannot exceed 255 characters.';
    }
    if (!canSubmit) {
      return 'Please confirm the location and attached GPS coordinates.';
    }
    return null;
  }

  @override
  void dispose() {
    _disposed = true;
    _generation++;
    text.dispose();
    super.dispose();
  }
}
