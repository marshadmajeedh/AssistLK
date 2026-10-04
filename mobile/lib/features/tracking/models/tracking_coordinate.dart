import 'package:latlong2/latlong.dart';

class TrackingCoordinate {
  final String jobId;
  final double latitude;
  final double longitude;
  final DateTime? updatedAtUtc;

  const TrackingCoordinate({
    required this.jobId,
    required this.latitude,
    required this.longitude,
    this.updatedAtUtc,
  });

  LatLng get point => LatLng(latitude, longitude);

  factory TrackingCoordinate.fromSignalR(Object? value) {
    if (value is! Map) {
      throw const FormatException('Tracking update is not an object.');
    }

    final data = <String, dynamic>{
      for (final entry in value.entries)
        entry.key.toString().toLowerCase(): entry.value,
    };
    final latitude = (data['latitude'] as num?)?.toDouble();
    final longitude = (data['longitude'] as num?)?.toDouble();
    if (latitude == null || longitude == null) {
      throw const FormatException('Tracking update has no coordinates.');
    }

    final rawUpdatedAt = data['updatedatutc']?.toString();
    return TrackingCoordinate(
      jobId: data['jobid']?.toString() ?? '',
      latitude: latitude,
      longitude: longitude,
      updatedAtUtc: rawUpdatedAt == null
          ? null
          : DateTime.tryParse(rawUpdatedAt),
    );
  }
}