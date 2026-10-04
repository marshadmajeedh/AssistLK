import 'package:dio/dio.dart';

import '../../../core/api/api_client.dart';
import '../models/booking_model.dart';
import '../models/quotation_model.dart';

/// HTTP service for Component 3 — Quotation & Booking.
/// Wraps all calls to /api/quotations/* on the ASP.NET Core backend.
class QuotationService {
  final ApiClient apiClient;

  QuotationService({required this.apiClient});

  /// GET /api/quotations/by-request/{serviceRequestId}
  Future<List<QuotationModel>> listForServiceRequest(
    String serviceRequestId, {
    CancelToken? cancelToken,
  }) async {
    final response = await apiClient.client.get(
      '/quotations/by-request/$serviceRequestId',
      cancelToken: cancelToken,
    );
    final list = response.data as List<dynamic>;
    return list
        .map(
          (e) => QuotationModel.fromJson(
            Map<String, dynamic>.from(e as Map),
          ),
        )
        .toList();
  }

  /// GET /api/quotations/{id}
  Future<QuotationModel> getById(
    int quotationId, {
    CancelToken? cancelToken,
  }) async {
    final response = await apiClient.client.get(
      '/quotations/$quotationId',
      cancelToken: cancelToken,
    );
    return QuotationModel.fromJson(
      Map<String, dynamic>.from(response.data as Map),
    );
  }

  /// POST /api/quotations/{id}/approve
  /// Atomic: approves the quotation and creates a Booking on the backend.
  Future<BookingModel> approve(
    int quotationId, {
    required String threadId,
    String? customerRemarks,
    CancelToken? cancelToken,
  }) async {
    final response = await apiClient.client.post(
      '/quotations/$quotationId/approve',
      data: {
        'customerRemarks': customerRemarks,
        'threadId': threadId,
      },
      cancelToken: cancelToken,
    );
    return BookingModel.fromJson(
      Map<String, dynamic>.from(response.data as Map),
    );
  }

  /// POST /api/quotations/{id}/reject
  Future<QuotationModel> reject(
    int quotationId, {
    required String threadId,
    required String reason,
    CancelToken? cancelToken,
  }) async {
    final response = await apiClient.client.post(
      '/quotations/$quotationId/reject',
      data: {
        'reason': reason,
        'threadId': threadId,
      },
      cancelToken: cancelToken,
    );
    return QuotationModel.fromJson(
      Map<String, dynamic>.from(response.data as Map),
    );
  }

  /// Maps Dio exceptions to user-friendly messages.
  /// Mirrors the style of ServiceRequestService.getErrorMessage.
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

      switch (error.response?.statusCode) {
        case 400:
          return 'This quotation could not be processed. Please refresh and try again.';
        case 401:
          return 'Your session has ended. Please sign in again.';
        case 403:
          return 'You do not have permission to perform this action.';
        case 404:
          return 'Quotation not found.';
        case 409:
          return 'This quotation is no longer awaiting your approval.';
        case 500:
        case 503:
          return 'AssistLK is temporarily unavailable. Please try again in a moment.';
      }
    }

    return 'Something went wrong. Please try again.';
  }
}