import 'dart:async';
import 'dart:typed_data';

import 'package:dio/dio.dart';

import '../../../core/api/api_client.dart';
import '../models/create_service_request_dto.dart';
import '../models/problem_understanding_result_model.dart';
import '../models/service_request_clarification_model.dart';
import '../models/service_request_model.dart';
import '../models/submit_clarification_answers_dto.dart';
import '../models/update_service_request_dto.dart';
import '../models/problem_photo.dart';
import '../models/service_request_attachment.dart';

class ServiceRequestService {
  final ApiClient apiClient;

  ServiceRequestService({required this.apiClient});

  Future<ServiceRequestAttachment> uploadAttachment(
    String id,
    ProblemPhoto photo, {
    CancelToken? cancelToken,
  }) async {
    final size = await photo.file.length();
    final response = await apiClient.client.post(
      '/service-requests/$id/attachments',
      data: FormData.fromMap({
        'file': MultipartFile.fromStream(
          () => photo.file.openRead(),
          size,
          filename: 'problem.${photo.extension}',
          contentType: DioMediaType.parse(
            photo.mimeType ?? 'application/octet-stream',
          ),
        ),
      }),
      options: Options(
        contentType: 'multipart/form-data',
        sendTimeout: const Duration(seconds: 60),
        receiveTimeout: const Duration(seconds: 60),
      ),
      cancelToken: cancelToken,
    );
    return ServiceRequestAttachment.fromJson(
      Map<String, dynamic>.from(response.data as Map),
    );
  }

  Future<List<ServiceRequestAttachment>> listAttachments(
    String id, {
    CancelToken? cancelToken,
  }) async {
    final response = await apiClient.client.get(
      '/service-requests/$id/attachments',
      cancelToken: cancelToken,
    );
    return (response.data as List)
        .map(
          (e) => ServiceRequestAttachment.fromJson(
            Map<String, dynamic>.from(e as Map),
          ),
        )
        .toList()
      ..sort((a, b) => a.slot.compareTo(b.slot));
  }

  Future<Uint8List> getAttachmentContent(
    String id,
    String attachmentId, {
    CancelToken? cancelToken,
  }) async {
    final response = await apiClient.client.get<List<int>>(
      '/service-requests/$id/attachments/$attachmentId/content',
      options: Options(responseType: ResponseType.bytes),
      cancelToken: cancelToken,
    );
    return Uint8List.fromList(response.data!);
  }

  Future<void> deleteAttachment(
    String id,
    String attachmentId, {
    CancelToken? cancelToken,
  }) async {
    await apiClient.client.delete(
      '/service-requests/$id/attachments/$attachmentId',
      cancelToken: cancelToken,
    );
  }

  String photoError(Object error) {
    if (error is DioException) {
      switch (error.response?.statusCode) {
        case 400:
          return 'Photo could not be accepted. Use a valid JPEG, PNG or static WebP under 5 MiB.';
        case 413:
          return 'Choose a photo smaller than 5 MiB.';
        case 409:
          return 'Photo limit reached or request no longer editable. Refresh the request.';
        case 401:
          return 'Your session has ended. Please sign in again.';
        case 403:
          return 'You do not have permission to access these photos.';
        case 404:
          return 'This request or photo is no longer available.';
      }
    }
    return 'Could not complete the photo request. Check your connection and try again.';
  }

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
        .map(
          (e) =>
              ServiceRequestModel.fromJson(Map<String, dynamic>.from(e as Map)),
        )
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

  /// Service Request Analysis Endpoint
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

  /// FastAPI Agent 4 Validation Route එකට අදාළ Call එක
  Future<Map<String, dynamic>> validateTransition({
    required String jobId,
    required String currentStatus,
    required String targetStatus,
    required double elapsedMinutes,
    String note = '',
  }) async {
    final response = await apiClient.client.post(
      '/validation/validate-status',
      data: {
        'job_id': jobId,
        'current_status': currentStatus,
        'target_status': targetStatus,
        'elapsed_minutes': elapsedMinutes.round(),
        'note': note,
      },
      options: Options(
        receiveTimeout: const Duration(seconds: 90),
        sendTimeout: const Duration(seconds: 30),
      ),
    );

    return Map<String, dynamic>.from(response.data as Map);
  }

  Future<Map<String, dynamic>> updateRequestStatusToCompleted(
    String jobId, {
    double timeElapsedMinutes = 0,
    String notes = 'Work completed',
  }) async {
    final response = await apiClient.client.put(
      '/service-jobs/$jobId/status',
      data: {
        'newStatus': 'Completed',
        'timeElapsedMinutes': timeElapsedMinutes,
        'notes': notes,
      },
    );

    return Map<String, dynamic>.from(response.data as Map);
  }

  Future<Map<String, dynamic>> analyzeSentiment(String feedbackText) async {
    final response = await apiClient.client.post(
      '/validation/analyze-sentiment',
      data: {'feedback_text': feedbackText},
      options: Options(
        receiveTimeout: const Duration(seconds: 90),
        sendTimeout: const Duration(seconds: 30),
      ),
    );

    return Map<String, dynamic>.from(response.data as Map);
  }

  Future<ServiceRequestModel> markReadyForMatching(String id) async {
    final response = await apiClient.client.post(
      '/service-requests/$id/ready-for-matching',
    );

    return ServiceRequestModel.fromJson(
      Map<String, dynamic>.from(response.data as Map),
    );
  }

  Future<List<ServiceRequestClarificationModel>> submitClarificationAnswers(
    String id,
    SubmitClarificationAnswersDto dto,
  ) async {
    final response = await apiClient.client.post(
      '/service-requests/$id/clarifications/answers',
      data: dto.toJson(),
    );

    final dataList = response.data as List<dynamic>;
    return dataList
        .map(
          (e) => ServiceRequestClarificationModel.fromJson(
            Map<String, dynamic>.from(e as Map),
          ),
        )
        .toList();
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
        return 'Invalid validation request details.';
      }

      if (error.response?.statusCode == 404) {
        return 'Validation endpoint not found (404).';
      }

      if (error.response?.statusCode == 403) {
        return 'You do not have permission to perform this action.';
      }
    }

    return 'Validation could not be completed. Please try again.';
  }
}
