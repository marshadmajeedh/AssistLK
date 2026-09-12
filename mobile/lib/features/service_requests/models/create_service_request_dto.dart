import 'location_source.dart';

class CreateServiceRequestDto {
  final String description;
  final String locationText;
  final LocationSource locationSource;
  final double? latitude;
  final double? longitude;
  final String? categoryHint;

  const CreateServiceRequestDto({
    required this.description,
    required this.locationText,
    this.locationSource = LocationSource.manual,
    this.latitude,
    this.longitude,
    this.categoryHint,
  });

  Map<String, dynamic> toJson() {
    return {
      'description': description.trim(),
      'locationSource': locationSource.value,
      'locationText': locationText.trim(),
      if (latitude != null) 'latitude': latitude,
      if (longitude != null) 'longitude': longitude,
      if (categoryHint != null) 'categoryHint': categoryHint,
    };
  }

  String? validate() {
    if (description.trim().isEmpty) {
      return 'Description cannot be empty.';
    }
    if (description.trim().length > 4000) {
      return 'Description cannot exceed 4000 characters.';
    }
    if (locationText.trim().isEmpty) {
      return 'Location cannot be empty.';
    }
    if (locationText.trim().length > 255) {
      return 'Location cannot exceed 255 characters.';
    }
    if ((latitude == null) != (longitude == null)) {
      return 'Both latitude and longitude must be provided together.';
    }
    if (latitude != null && (latitude! < -90 || latitude! > 90)) {
      return 'Latitude must be between -90 and 90.';
    }
    if (longitude != null && (longitude! < -180 || longitude! > 180)) {
      return 'Longitude must be between -180 and 180.';
    }
    return null;
  }
}
