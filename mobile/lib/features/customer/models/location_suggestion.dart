import '../../service_requests/models/location_source.dart';
import '../../service_requests/models/resolved_location.dart';

/// A capture snapshot, never a request draft or a request confirmation.
class LocationSuggestion {
  static const maxAge = Duration(minutes: 15);

  final ResolvedLocation location;
  final double? latitude;
  final double? longitude;
  final double? accuracyMeters;
  final LocationSource source;
  final DateTime capturedAt;

  const LocationSuggestion({
    required this.location,
    required this.latitude,
    required this.longitude,
    required this.source,
    required this.capturedAt,
    this.accuracyMeters,
  });

  bool isFresh(DateTime now) {
    final age = now.difference(capturedAt);
    return !age.isNegative && age < maxAge;
  }
}
