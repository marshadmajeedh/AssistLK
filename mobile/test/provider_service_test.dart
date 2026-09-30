import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/api/api_client.dart';
import 'package:mobile/core/auth/token_storage.dart';
import 'package:mobile/features/providers/services/provider_service.dart';

class _FakeTokenStorage extends TokenStorage {
  @override
  Future<String?> getToken() async => 'mock_provider_token';
}

void main() {
  group('ProviderService Unit Tests (Component 2: Matching Dispatch & Availability)', () {
    test('getProfile sends GET to /providers/profile and parses result', () async {
      final dio = Dio();
      dio.interceptors.add(
        InterceptorsWrapper(
          onRequest: (options, handler) {
            expect(options.method, 'GET');
            expect(options.path, '/providers/profile');
            handler.resolve(
              Response(
                requestOptions: options,
                statusCode: 200,
                data: {
                  'id': 'prov-42',
                  'fullName': 'Kasun Perera',
                  'businessName': 'Kasun Plumbing & Electrical',
                  'rating': 4.9,
                  'verificationStatus': 'Verified',
                  'skills': [
                    {'category': 'Plumbing'},
                    {'category': 'Electrical'},
                  ],
                },
              ),
            );
          },
        ),
      );

      final apiClient = ApiClient(dio: dio, tokenStorage: _FakeTokenStorage());
      final service = ProviderService(apiClient: apiClient);

      final profile = await service.getProfile();
      expect(profile['id'], 'prov-42');
      expect(profile['fullName'], 'Kasun Perera');
      expect(profile['rating'], 4.9);
      expect(profile['verificationStatus'], 'Verified');
      expect((profile['skills'] as List).length, 2);
    });

    test('updateProfile sends PUT to /providers/profile with optional fields', () async {
      final dio = Dio();
      dio.interceptors.add(
        InterceptorsWrapper(
          onRequest: (options, handler) {
            expect(options.method, 'PUT');
            expect(options.path, '/providers/profile');
            final data = options.data as Map<String, dynamic>;
            expect(data['businessName'], 'Kasun Pro Services');
            expect(data['operatingRadiusKm'], 25.0);
            expect(data['latitude'], 6.9271);
            expect(data['longitude'], 79.8612);

            handler.resolve(
              Response(
                requestOptions: options,
                statusCode: 200,
                data: {
                  'businessName': 'Kasun Pro Services',
                  'operatingRadiusKm': 25.0,
                  'latitude': 6.9271,
                  'longitude': 79.8612,
                },
              ),
            );
          },
        ),
      );

      final apiClient = ApiClient(dio: dio, tokenStorage: _FakeTokenStorage());
      final service = ProviderService(apiClient: apiClient);

      final result = await service.updateProfile(
        businessName: 'Kasun Pro Services',
        operatingRadiusKm: 25.0,
        latitude: 6.9271,
        longitude: 79.8612,
      );

      expect(result['businessName'], 'Kasun Pro Services');
      expect(result['operatingRadiusKm'], 25.0);
    });

    test('updateAvailability sends PUT to /providers/availability with GPS coordinates and online status',
        () async {
      final dio = Dio();
      bool requestReceived = false;

      dio.interceptors.add(
        InterceptorsWrapper(
          onRequest: (options, handler) {
            expect(options.method, 'PUT');
            expect(options.path, '/providers/availability');
            final data = options.data as Map<String, dynamic>;
            expect(data['isOnline'], true);
            expect(data['latitude'], 6.9271);
            expect(data['longitude'], 79.8612);
            expect(data['operatingRadiusKm'], 15.0);
            requestReceived = true;

            handler.resolve(
              Response(
                requestOptions: options,
                statusCode: 200,
                data: {'status': 'updated'},
              ),
            );
          },
        ),
      );

      final apiClient = ApiClient(dio: dio, tokenStorage: _FakeTokenStorage());
      final service = ProviderService(apiClient: apiClient);

      await service.updateAvailability(
        isOnline: true,
        latitude: 6.9271,
        longitude: 79.8612,
        operatingRadiusKm: 15.0,
      );

      expect(requestReceived, isTrue);
    });

    test('acceptActiveDispatch sends POST request to active-dispatch accept route', () async {
      final dio = Dio();
      bool accepted = false;

      dio.interceptors.add(
        InterceptorsWrapper(
          onRequest: (options, handler) {
            expect(options.method, 'POST');
            expect(options.path.contains('active-dispatch/accept'), isTrue);
            accepted = true;

            handler.resolve(
              Response(
                requestOptions: options,
                statusCode: 200,
                data: {'status': 'Accepted'},
              ),
            );
          },
        ),
      );

      final apiClient = ApiClient(dio: dio, tokenStorage: _FakeTokenStorage());
      final service = ProviderService(apiClient: apiClient);

      await service.acceptActiveDispatch();
      expect(accepted, isTrue);
    });

    test('declineActiveDispatch sends POST request to active-dispatch decline route', () async {
      final dio = Dio();
      bool declined = false;

      dio.interceptors.add(
        InterceptorsWrapper(
          onRequest: (options, handler) {
            expect(options.method, 'POST');
            expect(options.path.contains('active-dispatch/decline'), isTrue);
            declined = true;

            handler.resolve(
              Response(
                requestOptions: options,
                statusCode: 200,
                data: {'status': 'Declined'},
              ),
            );
          },
        ),
      );

      final apiClient = ApiClient(dio: dio, tokenStorage: _FakeTokenStorage());
      final service = ProviderService(apiClient: apiClient);

      await service.declineActiveDispatch();
      expect(declined, isTrue);
    });

    test('propagates DioException when server reports error', () async {
      final dio = Dio();
      dio.interceptors.add(
        InterceptorsWrapper(
          onRequest: (options, handler) {
            handler.reject(
              DioException(
                requestOptions: options,
                response: Response(
                  requestOptions: options,
                  statusCode: 404,
                  data: {'message': 'No active dispatch found for this provider.'},
                ),
                type: DioExceptionType.badResponse,
              ),
            );
          },
        ),
      );

      final apiClient = ApiClient(dio: dio, tokenStorage: _FakeTokenStorage());
      final service = ProviderService(apiClient: apiClient);

      expect(
        () => service.acceptActiveDispatch(),
        throwsA(isA<DioException>()),
      );
    });
  });
}
