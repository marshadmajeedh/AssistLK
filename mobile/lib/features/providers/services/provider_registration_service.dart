import 'dart:convert';
import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../../../core/config/app_config.dart';

class ProviderRegistrationService {
  final Dio _dio;

  ProviderRegistrationService({Dio? dio})
      : _dio = dio ??
            Dio(
              BaseOptions(
                baseUrl: AppConfig.apiBaseUrl,
                connectTimeout: const Duration(seconds: 20),
                receiveTimeout: const Duration(seconds: 20),
              ),
            );

  Future<String?> uploadCertificate({
    String? filePath,
    Uint8List? bytes,
    String? fileName,
  }) async {
    try {
      final MultipartFile multipartFile;
      if (bytes != null && bytes.isNotEmpty) {
        multipartFile = MultipartFile.fromBytes(
          bytes,
          filename: fileName ?? 'certificate.pdf',
        );
      } else if (filePath != null && filePath.isNotEmpty) {
        multipartFile = await MultipartFile.fromFile(
          filePath,
          filename: fileName ?? filePath.split(RegExp(r'[\\/]')).last,
        );
      } else {
        throw ArgumentError('Either bytes or filePath must be provided');
      }

      final formData = FormData.fromMap({
        'file': multipartFile,
      });

      final response =
          await _dio.post('/providers/upload-certificate', data: formData);
      if (response.statusCode == 200 && response.data is Map) {
        return response.data['fileUrl'] as String?;
      }
      return null;
    } catch (e) {
      debugPrint('Upload error: $e');
      throw Exception('Failed to upload certificate: $e');
    }
  }

  Future<void> registerProvider(Map<String, dynamic> data) async {
    try {
      final response = await _dio.post(
        '/providers/register',
        data: jsonEncode(data),
        options: Options(headers: {'Content-Type': 'application/json'}),
      );
      if (response.statusCode != 200) {
        throw Exception('Failed to register provider');
      }
    } catch (e) {
      debugPrint('Registration error: $e');
      throw Exception('Failed to register provider: $e');
    }
  }
}
