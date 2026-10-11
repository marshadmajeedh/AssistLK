import '../../../core/api/api_client.dart';
import '../models/forward_geocode_candidate.dart';
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

  Future<List<ForwardGeocodeCandidate>> forwardGeocode(String address) async {
    final response = await apiClient.client.post(
      '/location/forward-geocode',
      data: {'address': address},
    );
    final data = response.data;
    if (data is List) {
      return data
          .map((item) => ForwardGeocodeCandidate.fromJson(
                Map<String, dynamic>.from(item as Map),
              ))
          .toList();
    }
    return [];
  }
}
