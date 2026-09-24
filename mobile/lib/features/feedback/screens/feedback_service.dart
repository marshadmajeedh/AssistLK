import 'dart:convert';

import 'package:http/http.dart' as http;

// ඔයාගේ project එකේ AppConfig file එක තියෙන path එකට මේක වෙනස් කරගන්න.
// (Agent දුන්න path එක තමයි මේ තියෙන්නේ)
import '../../../core/config/app_config.dart';

class FeedbackSubmissionException implements Exception {
  final String message;

  const FeedbackSubmissionException(this.message);
}

class FeedbackService {
  // Hardcode කරපු URL එක වෙනුවට AppConfig.apiBaseUrl ලබා ගැනීම
  static String get baseUrl => '${AppConfig.apiBaseUrl}/service-jobs';

  Future<Map<String, dynamic>?> submitFeedback({
    required String jobId,
    required String customerId,
    required int rating,
    required String comment,
  }) async {
    final url = Uri.parse('$baseUrl/$jobId/feedback');
    try {
      final response = await http.post(
        url,
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({
          'customerId': customerId,
          'rating': rating,
          'comment': comment,
        }),
      );

      if (response.statusCode == 200) {
        return jsonDecode(
          response.body,
        ); // JSON එක Map එකක් විදිහට return කිරීම
      } else if (response.statusCode == 400) {
        final responseBody = jsonDecode(response.body);
        if (responseBody is Map<String, dynamic> &&
            responseBody['message'] is String &&
            (responseBody['message'] as String).isNotEmpty) {
          throw FeedbackSubmissionException(responseBody['message'] as String);
        }
      } else {
        print('Failed. Status Code: ${response.statusCode}');
      }
    } on FeedbackSubmissionException {
      rethrow;
    } catch (e) {
      print('Connection Error: $e');
    }
    return null;
  }
}
