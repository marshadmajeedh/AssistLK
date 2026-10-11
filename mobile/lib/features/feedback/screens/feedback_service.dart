import 'package:dio/dio.dart';

import '../../../core/api/api_client.dart';

class FeedbackSubmissionException implements Exception {
  final String message;

  const FeedbackSubmissionException(this.message);
}

class FeedbackService {
  final ApiClient _apiClient;

  FeedbackService({ApiClient? apiClient}) : _apiClient = apiClient ?? ApiClient();

  Future<Map<String, dynamic>?> submitFeedback({
    required String jobId,
    required int rating,
    required String comment,
  }) async {
    try {
      final response = await _apiClient.client.post(
        '/service-jobs/$jobId/feedback',
        data: {
          'rating': rating,
          'comment': comment,
        },
      );

      if (response.data is Map) {
        return Map<String, dynamic>.from(response.data as Map);
      }

      throw const FeedbackSubmissionException(
        'The feedback response was invalid. Please try again.',
      );
    } on DioException catch (error) {
      final statusCode = error.response?.statusCode;
      if (statusCode == 409) {
        throw const FeedbackSubmissionException(
          'Feedback already submitted for this service job.',
        );
      }

      if (statusCode == 500) {
        throw const FeedbackSubmissionException(
          'The server failed to process the request. Check the API logs.',
        );
      }

      final responseData = error.response?.data;
      if (responseData is Map && responseData['message'] is String) {
        throw FeedbackSubmissionException(responseData['message'] as String);
      }

      throw FeedbackSubmissionException(
        statusCode == null
            ? 'Unable to connect to the server. Please try again.'
            : 'Failed to submit feedback. Please try again.',
      );
    }
  }
}
