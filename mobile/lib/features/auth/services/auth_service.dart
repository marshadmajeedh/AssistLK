import 'package:dio/dio.dart';

import '../../../core/api/api_client.dart';
import '../models/auth_result.dart';
import '../models/auth_user.dart';

class AuthService {
  final ApiClient apiClient;

  AuthService({required this.apiClient});

  Future<AuthResult> login({
    required String email,
    required String password,
  }) async {
    final response = await apiClient.client.post(
      '/auth/login',
      data: {'email': email.trim(), 'password': password},
    );

    return AuthResult.fromJson(Map<String, dynamic>.from(response.data));
  }

  Future<AuthResult> register({
    required String fullName,
    required String email,
    required String password,
    required String phoneNumber,
    required String role,
  }) async {
    final response = await apiClient.client.post(
      '/auth/register',
      data: {
        'fullName': fullName.trim(),
        'email': email.trim(),
        'password': password,
        'phoneNumber': phoneNumber.trim(),
        'role': role,
      },
    );

    return AuthResult.fromJson(Map<String, dynamic>.from(response.data));
  }

  Future<AuthUser> getMe() async {
    final response = await apiClient.client.get('/auth/me');

    return AuthUser.fromProfile(Map<String, dynamic>.from(response.data));
  }

  String getErrorMessage(Object error) {
    if (error is DioException) {
      final data = error.response?.data;

      if (data is Map && data['message'] != null) {
        return data['message'].toString();
      }

      if (error.type == DioExceptionType.connectionTimeout) {
        return 'Connection timed out.';
      }

      if (error.type == DioExceptionType.connectionError) {
        return 'Unable to connect to AssistLK server.';
      }
    }

    return 'Something went wrong. Please try again.';
  }
}
