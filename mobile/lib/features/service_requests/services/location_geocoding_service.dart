import '../../../core/api/api_client.dart';
import '../models/resolved_location.dart';

class LocationGeocodingService {
  final ApiClient apiClient;
  LocationGeocodingService({required this.apiClient});

  Future<ResolvedLocation> reverseGeocode(
    double latitude,
    double longitude,
  ) async {
    final response = await apiClient.client.post(
      '/location/reverse-geocode',
      data: {'latitude': latitude, 'longitude': longitude},
    );
    return ResolvedLocation.fromJson(
      Map<String, dynamic>.from(response.data as Map),
    );
  }
}
