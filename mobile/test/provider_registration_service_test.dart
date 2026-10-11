import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/providers/services/provider_registration_service.dart';

void main() {
  group('ProviderRegistrationService Unit Tests', () {
    test('uploadCertificate with valid bytes sends request and returns fileUrl', () async {
      final dio = Dio();
      dio.interceptors.add(
        InterceptorsWrapper(
          onRequest: (options, handler) {
            expect(options.path, '/providers/upload-certificate');
            expect(options.data, isA<FormData>());
            handler.resolve(
              Response(
                requestOptions: options,
                statusCode: 200,
                data: {'fileUrl': '/uploads/certificates/license-001.pdf'},
              ),
            );
          },
        ),
      );

      final service = ProviderRegistrationService(dio: dio);
      final fileUrl = await service.uploadCertificate(
        bytes: Uint8List.fromList([1, 2, 3, 4]),
        fileName: 'license-001.pdf',
      );

      expect(fileUrl, '/uploads/certificates/license-001.pdf');
    });

    test('uploadCertificate throws ArgumentError when no bytes or path given', () async {
      final dio = Dio();
      final service = ProviderRegistrationService(dio: dio);

      expect(
        () => service.uploadCertificate(),
        throwsA(isA<Exception>()),
      );
    });

    test('uploadCertificate handles server failure and throws Exception', () async {
      final dio = Dio();
      dio.interceptors.add(
        InterceptorsWrapper(
          onRequest: (options, handler) {
            handler.reject(
              DioException(
                requestOptions: options,
                type: DioExceptionType.badResponse,
                response: Response(
                  requestOptions: options,
                  statusCode: 500,
                  data: {'message': 'Storage disk full'},
                ),
              ),
            );
          },
        ),
      );

      final service = ProviderRegistrationService(dio: dio);

      expect(
        () => service.uploadCertificate(
          bytes: Uint8List.fromList([37, 80, 68, 70]),
          fileName: 'cert.pdf',
        ),
        throwsA(isA<Exception>()),
      );
    });

    test('registerProvider sends POST to /providers/register with correct JSON', () async {
      final dio = Dio();
      Map<String, dynamic>? capturedBody;

      dio.interceptors.add(
        InterceptorsWrapper(
          onRequest: (options, handler) {
            expect(options.path, '/providers/register');
            expect(options.headers['Content-Type'], 'application/json');
            capturedBody = jsonDecode(options.data as String) as Map<String, dynamic>;
            handler.resolve(
              Response(
                requestOptions: options,
                statusCode: 200,
                data: {'message': 'Provider registered successfully'},
              ),
            );
          },
        ),
      );

      final service = ProviderRegistrationService(dio: dio);
      final payload = {
        'fullName': 'Kamal Gunaratne',
        'email': 'kamal@example.com',
        'password': 'Password@123',
        'phoneNumber': '0771234567',
        'businessName': 'Kamal Plumbing & Electric',
        'latitude': 6.9271,
        'longitude': 79.8612,
        'operatingRadiusKm': 15.0,
        'skills': [
          {'category': 'Plumbing', 'skillName': 'Pipe Fitting', 'certificationUrl': '/uploads/p.pdf'},
          {'category': 'Electrical', 'skillName': 'Rewiring', 'certificationUrl': '/uploads/e.pdf'}
        ]
      };

      await service.registerProvider(payload);

      expect(capturedBody, isNotNull);
      expect(capturedBody!['email'], 'kamal@example.com');
      expect(capturedBody!['operatingRadiusKm'], 15.0);
      expect(capturedBody!['skills'], hasLength(2));
    });

    test('registerProvider extracts and displays server error message on failure', () async {
      final dio = Dio();
      dio.interceptors.add(
        InterceptorsWrapper(
          onRequest: (options, handler) {
            handler.reject(
              DioException(
                requestOptions: options,
                type: DioExceptionType.badResponse,
                response: Response(
                  requestOptions: options,
                  statusCode: 400,
                  data: {'message': 'Email already registered.'},
                ),
              ),
            );
          },
        ),
      );

      final service = ProviderRegistrationService(dio: dio);

      expect(
        () => service.registerProvider({'email': 'existing@example.com'}),
        throwsA(
          predicate((e) => e is Exception && e.toString().contains('Email already registered.')),
        ),
      );
    });
  });
}
