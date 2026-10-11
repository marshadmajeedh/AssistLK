import '../../../core/api/api_client.dart';
import 'package:dio/dio.dart';
import 'package:image_picker/image_picker.dart';

class ProviderService {
  final ApiClient apiClient;

  ProviderService({required this.apiClient});

  Future<Map<String, dynamic>> getProfile() async {
    final response = await apiClient.client.get('/providers/profile');
    return Map<String, dynamic>.from(response.data as Map);
  }

  Future<Map<String, dynamic>> updateProfile({
    String? businessName,
    double? operatingRadiusKm,
    double? latitude,
    double? longitude,
  }) async {
    final payload = <String, dynamic>{};
    if (businessName != null) payload['businessName'] = businessName;
    if (operatingRadiusKm != null) payload['operatingRadiusKm'] = operatingRadiusKm;
    if (latitude != null) payload['latitude'] = latitude;
    if (longitude != null) payload['longitude'] = longitude;

    final response = await apiClient.client.put(
      '/providers/profile',
      data: payload,
    );
    return Map<String, dynamic>.from(response.data as Map);
  }

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

  Future<void> updateJobStatus(String jobId, String newStatus) async {
    final response = await apiClient.client.put(
      '/service-jobs/$jobId/status',
      data: {'newStatus': newStatus},
    );

    final statusCode = response.statusCode;
    if (statusCode == null || statusCode < 200 || statusCode >= 300) {
      throw DioException(
        requestOptions: response.requestOptions,
        response: response,
        message: 'Status update failed with HTTP $statusCode.',
      );
    }
  }

  Future<Map<String, dynamic>> completeJob(
    String jobId, {
    double timeElapsedMinutes = 0,
    String notes = 'Work completed',
    XFile? proofOfWorkImage,
  }) async {
    final formData = FormData.fromMap({
      'notes': notes,
      'timeElapsedMinutes': timeElapsedMinutes,
    });

    if (proofOfWorkImage != null) {
      formData.files.add(
        MapEntry(
          'proofOfWorkImage',
          MultipartFile.fromBytes(
            await proofOfWorkImage.readAsBytes(),
            filename: proofOfWorkImage.name,
          ),
        ),
      );
    }

    final response = await apiClient.client.put(
      '/service-jobs/$jobId/complete',
      data: formData,
    );

    return Map<String, dynamic>.from(response.data as Map);
  }
}
