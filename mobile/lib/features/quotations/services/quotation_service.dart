import 'package:dio/dio.dart';

import '../../../core/api/api_client.dart';
import '../models/booking_model.dart';
import '../models/quotation_item_model.dart';
import '../models/quotation_model.dart';
import '../models/quotation_status.dart';

/// HTTP service for Component 3 — Quotation & Booking.
/// Wraps all calls to /api/quotations/* and /api/provider/* on the backend.
class QuotationService {
  final ApiClient apiClient;

  QuotationService({required this.apiClient});

  // ─────────────────────────────────────────────────────────────
  // CUSTOMER SIDE
  // ─────────────────────────────────────────────────────────────

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
          (e) => QuotationModel.fromJson(Map<String, dynamic>.from(e as Map)),
        )
        .toList();
  }

  /// GET /api/quotations/mine
  Future<List<QuotationModel>> listForCurrentCustomer({
    CancelToken? cancelToken,
  }) async {
    final response = await apiClient.client.get(
      '/quotations/mine',
      cancelToken: cancelToken,
    );
    final list = response.data as List<dynamic>;
    return list
        .map(
          (e) => QuotationModel.fromJson(Map<String, dynamic>.from(e as Map)),
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
  Future<BookingModel> approve(
    int quotationId, {
    required String threadId,
    String? customerRemarks,
    CancelToken? cancelToken,
  }) async {
    final response = await apiClient.client.post(
      '/quotations/$quotationId/approve',
      data: {'customerRemarks': customerRemarks, 'threadId': threadId},
      options: Options(receiveTimeout: const Duration(seconds: 75)),
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
      data: {'reason': reason, 'threadId': threadId},
      options: Options(receiveTimeout: const Duration(seconds: 75)),
      cancelToken: cancelToken,
    );
    return QuotationModel.fromJson(
      Map<String, dynamic>.from(response.data as Map),
    );
  }

  // ─────────────────────────────────────────────────────────────
  // PROVIDER SIDE
  // ─────────────────────────────────────────────────────────────

  /// GET /api/provider/bookings?filter=upcoming|completed
  Future<List<ProviderBookingSummaryModel>> getProviderBookings({
    bool completed = false,
    CancelToken? cancelToken,
  }) async {
    final response = await apiClient.client.get(
      '/provider/bookings',
      queryParameters: {'filter': completed ? 'completed' : 'upcoming'},
      cancelToken: cancelToken,
    );
    final list = response.data as List<dynamic>;
    return list
        .map(
          (e) => ProviderBookingSummaryModel.fromJson(
            Map<String, dynamic>.from(e as Map),
          ),
        )
        .toList();
  }

  /// GET /api/provider/bookings/{id}
  Future<ProviderBookingSummaryModel> getProviderBookingById(
    int bookingId, {
    CancelToken? cancelToken,
  }) async {
    final response = await apiClient.client.get(
      '/provider/bookings/$bookingId',
      cancelToken: cancelToken,
    );
    return ProviderBookingSummaryModel.fromJson(
      Map<String, dynamic>.from(response.data as Map),
    );
  }

  /// PATCH /api/provider/bookings/{id}/status
  Future<void> updateProviderBookingStatus(
    int bookingId, {
    required BookingStatus newStatus,
    String? reason,
    CancelToken? cancelToken,
  }) async {
    await apiClient.client.patch(
      '/provider/bookings/$bookingId/status',
      data: {
        'newStatus': newStatus.code,
        'reason': reason ?? 'Updated via provider app',
      },
      cancelToken: cancelToken,
    );
  }

  /// GET /api/provider/bookings/{id}/history
  Future<List<dynamic>> getProviderBookingHistory(
    int bookingId, {
    CancelToken? cancelToken,
  }) async {
    final response = await apiClient.client.get(
      '/provider/bookings/$bookingId/history',
      cancelToken: cancelToken,
    );
    return response.data as List<dynamic>;
  }

  /// POST /api/provider/quotations
  Future<QuotationModel> createProviderQuotation({
    required String serviceRequestId,
    required String providerId,
    required List<QuotationItemModel> items,
    String? notes,
    CancelToken? cancelToken,
  }) async {
    final response = await apiClient.client.post(
      '/quotations',
      data: {
        'serviceRequestId': serviceRequestId,
        'providerId': providerId,
        'items': items
            .map(
              (item) => {
                'description': item.description,
                'amount': item.amount,
                'quantity': item.quantity,
              },
            )
            .toList(),
        'notes': notes ?? '',
      },
      cancelToken: cancelToken,
    );
    return QuotationModel.fromJson(
      Map<String, dynamic>.from(response.data as Map),
    );
  }

  /// POST /api/quotations/{id}/send-for-approval
  /// The ASP.NET endpoint starts the Python quotation-booking approval workflow.
  Future<QuotationModel> sendProviderQuotation(
    int quotationId, {
    CancelToken? cancelToken,
  }) async {
    final response = await apiClient.client.post(
      '/quotations/$quotationId/send-for-approval',
      options: Options(receiveTimeout: const Duration(seconds: 75)),
      cancelToken: cancelToken,
    );

    final body = Map<String, dynamic>.from(response.data as Map);
    final quotation = body['quotation'];
    if (quotation is! Map) {
      throw const FormatException(
        'Quotation approval workflow response did not include a quotation.',
      );
    }
    final quotationJson = Map<String, dynamic>.from(quotation);
    final approvalRequest = body['approvalRequest'];
    if (approvalRequest is Map && approvalRequest['riskAssessment'] is Map) {
      quotationJson['riskAssessment'] = approvalRequest['riskAssessment'];
    }

    return QuotationModel.fromJson(quotationJson);
  }

  // ─────────────────────────────────────────────────────────────
  // ERROR MAPPING
  // ─────────────────────────────────────────────────────────────

  String getErrorMessage(Object error) {
    if (error is DioException) {
      final data = error.response?.data;

      if (data is Map) {
        for (final key in ['message', 'detail', 'title']) {
          final message = data[key];
          if (message is String && message.trim().isNotEmpty) {
            return message;
          }
        }
        final errors = data['errors'];
        if (errors is Map) {
          final messages = errors.values
              .whereType<List>()
              .expand((value) => value)
              .map((value) => value.toString())
              .where((value) => value.trim().isNotEmpty)
              .toList();
          if (messages.isNotEmpty) return messages.join('\n');
        }
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
