import 'package:flutter/material.dart';

import '../../customer/models/location_suggestion.dart';

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
  LocationSuggestion? _suggestion;
  LocationSource _previewSource = LocationSource.openStreetMap;
  final DateTime Function() clock;
  bool get isSuggested => preview != null && _suggestion != null;
  LocationSource get previewSource => _previewSource;

  LocationSelectionController({
    required this.gps,
    required this.geocoding,
    String initialText = '',
    this.latitude,
    this.longitude,
    this.source = LocationSource.manual,
    LocationSuggestion? initialSuggestion,
    DateTime Function()? clock,
  }) : clock = clock ?? DateTime.now,
       text = TextEditingController(text: initialText) {
    _lastText = initialText;
    if (source == LocationSource.openStreetMap) _resolvedAddress = initialText;
    text.addListener(_edited);
    if (initialSuggestion != null && initialSuggestion.isFresh(this.clock())) {
      _suggestion = initialSuggestion;
      preview = initialSuggestion.location;
      _previewSource = initialSuggestion.source;
      latitude = initialSuggestion.latitude;
      longitude = initialSuggestion.longitude;
      accuracy = initialSuggestion.accuracyMeters;
      needsGpsChoice = hasGps;
      if (preview!.formattedAddress.length > 255) {
        message = 'This address exceeds 255 characters. Enter a shorter location manually.';
      }
    }
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
    _suggestion = null;
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
    _suggestion = null;
    _previewSource = LocationSource.openStreetMap;
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
    if (_suggestion != null && !_suggestion!.isFresh(clock())) {
      message = 'This location suggestion has expired. Refresh Location or Change Location.';
      notifyListeners();
      return;
    }
    _setText(address);
    source = _previewSource;
    _resolvedAddress = address;
    preview = null;
    _suggestion = null;
    needsGpsChoice = false;
    message = 'Location confirmed';
    notifyListeners();
  }

  void enterManually() {
    _suggestion = null;
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
    _suggestion = null;
    _generation++;
    busy = false;
    preview = null;
    latitude = longitude = accuracy = null;
    if (source == LocationSource.openStreetMap &&
        text.text == _resolvedAddress) {
      _setText('');
    }
    source = LocationSource.manual;
    needsGpsChoice = false;
    message = 'GPS coordinates removed';
    notifyListeners();
  }

  void cancelPending() {
    _suggestion = null;
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
