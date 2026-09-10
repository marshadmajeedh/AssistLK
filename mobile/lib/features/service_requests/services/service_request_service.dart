import 'dart:async';

import 'package:dio/dio.dart';

import '../../../core/api/api_client.dart';
import '../models/create_service_request_dto.dart';
import '../models/problem_understanding_result_model.dart';
import '../models/service_request_model.dart';
import '../models/update_service_request_dto.dart';

class ServiceRequestService {
  final ApiClient apiClient;

  ServiceRequestService({required this.apiClient});

  bool isTimeoutOrUncertainTransport(Object error) {
    if (error is DioException) {
      if (error.response != null) {
        return false;
      }
      return error.type == DioExceptionType.receiveTimeout ||
          error.type == DioExceptionType.connectionTimeout ||
          error.type == DioExceptionType.sendTimeout ||
          error.type == DioExceptionType.connectionError ||
          error.type == DioExceptionType.unknown;
    }
    return error is TimeoutException;
  }

  Future<ServiceRequestModel> create(CreateServiceRequestDto dto) async {
    final response = await apiClient.client.post(
      '/service-requests',
      data: dto.toJson(),
    );

    return ServiceRequestModel.fromJson(
      Map<String, dynamic>.from(response.data as Map),
    );
  }

  Future<List<ServiceRequestModel>> getMyRequests() async {
    final response = await apiClient.client.get('/service-requests/my');

    final dataList = response.data as List<dynamic>;
    return dataList
        .map((e) => ServiceRequestModel.fromJson(
            Map<String, dynamic>.from(e as Map)))
        .toList();
  }

  Future<ServiceRequestModel> getById(String id) async {
    final response = await apiClient.client.get('/service-requests/$id');

    return ServiceRequestModel.fromJson(
      Map<String, dynamic>.from(response.data as Map),
    );
  }

  Future<ServiceRequestModel> update(
    String id,
    UpdateServiceRequestDto dto,
  ) async {
    final response = await apiClient.client.put(
      '/service-requests/$id',
      data: dto.toJson(),
    );

    return ServiceRequestModel.fromJson(
      Map<String, dynamic>.from(response.data as Map),
    );
  }

  Future<ServiceRequestModel> cancel(String id) async {
    final response = await apiClient.client.post(
      '/service-requests/$id/cancel',
    );

    return ServiceRequestModel.fromJson(
      Map<String, dynamic>.from(response.data as Map),
    );
  }

  Future<ProblemUnderstandingResultModel> analyze(String id) async {
    final response = await apiClient.client.post(
      '/service-requests/$id/analyze',
      options: Options(
        receiveTimeout: const Duration(seconds: 90),
        sendTimeout: const Duration(seconds: 30),
      ),
    );

    return ProblemUnderstandingResultModel.fromJson(
      Map<String, dynamic>.from(response.data as Map),
    );
  }

  Future<ServiceRequestModel> markReadyForMatching(String id) async {
    final response = await apiClient.client.post(
      '/service-requests/$id/ready-for-matching',
    );

    return ServiceRequestModel.fromJson(
      Map<String, dynamic>.from(response.data as Map),
    );
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

      if (error.type == DioExceptionType.receiveTimeout ||
          error.type == DioExceptionType.sendTimeout) {
        return 'Request timed out. Please try again.';
      }

      if (error.type == DioExceptionType.connectionError) {
        return 'Unable to connect to AssistLK server.';
      }

      if (error.response?.statusCode == 400) {
        return 'Invalid request details. Please check your inputs.';
      }

      if (error.response?.statusCode == 404) {
        return 'Service request not found.';
      }

      if (error.response?.statusCode == 409) {
        return 'The request could not be completed due to a conflict.';
      }

      if (error.response?.statusCode == 403) {
        return 'You do not have permission to perform this action.';
      }
    }

    return 'Something went wrong. Please try again.';
  }

  String getAnalysisErrorMessage(Object error) {
    if (error is DioException) {
      if (error.type == DioExceptionType.receiveTimeout ||
          error.type == DioExceptionType.sendTimeout) {
        return 'Analysis is taking longer than expected. The request status has been refreshed.';
      }

      if (error.response?.statusCode == 409) {
        return 'Service request is currently being analyzed or in an updated status. The request status has been refreshed.';
      }

      final data = error.response?.data;
      if (data is Map && data['message'] != null) {
        return data['message'].toString();
      }

      if (error.type == DioExceptionType.connectionTimeout) {
        return 'Connection timed out while contacting analysis service.';
      }

      if (error.type == DioExceptionType.connectionError) {
        return 'Unable to connect to AssistLK server.';
      }

      if (error.response?.statusCode == 400) {
        return 'Invalid analysis request details.';
      }

      if (error.response?.statusCode == 404) {
        return 'Service request not found.';
      }

      if (error.response?.statusCode == 403) {
        return 'You do not have permission to analyze this request.';
      }
    }

    return 'Analysis could not be completed. Please try again.';
  }
}
