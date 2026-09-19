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

  Future<void> acceptActiveDispatch([String endpoint = '/api/providers/active-dispatch/accept']) async {
    final path = endpoint.startsWith('/api') && apiClient.client.options.baseUrl.endsWith('/api')
        ? endpoint.substring(4)
        : endpoint;
    await apiClient.client.post(path);
  }

  Future<void> declineActiveDispatch([String endpoint = '/api/providers/active-dispatch/decline']) async {
    final path = endpoint.startsWith('/api') && apiClient.client.options.baseUrl.endsWith('/api')
        ? endpoint.substring(4)
        : endpoint;
    await apiClient.client.post(path);
  }
}
