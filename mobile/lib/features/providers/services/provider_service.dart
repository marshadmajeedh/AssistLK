import '../../../core/api/api_client.dart';

class ProviderService {
  final ApiClient apiClient;

  ProviderService({required this.apiClient});

  Future<void> updateAvailability({
    required bool isOnline,
    required double latitude,
    required double longitude,
    required double operatingRadiusKm,
  }) async {
    await apiClient.client.put(
      '/providers/availability',
      data: {
        'isOnline': isOnline,
        'latitude': latitude,
        'longitude': longitude,
        'operatingRadiusKm': operatingRadiusKm,
      },
    );
  }
}
