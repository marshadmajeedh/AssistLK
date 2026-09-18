import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

import '../../../shared/theme/app_colors.dart';
import '../../../shared/theme/app_spacing.dart';
import '../../../shared/widgets/app_button.dart';

class FeedbackScreen extends StatefulWidget {
  final String jobId;

  const FeedbackScreen({super.key, required this.jobId});

  @override
  State<FeedbackScreen> createState() => _FeedbackScreenState();
}

class _FeedbackScreenState extends State<FeedbackScreen> {
  final TextEditingController _feedbackController = TextEditingController();
  bool _isLoading = false;

  @override
  void dispose() {
    _feedbackController.dispose();
    super.dispose();
  }

  Future<void> _submitFeedback() async {
    final text = _feedbackController.text.trim();
    if (text.isEmpty) {
      _showAlert('Attention', 'Please enter your feedback.');
      return;
    }

    setState(() => _isLoading = true);

    try {
      // Android Emulator සඳහා 10.0.2.2 ද, iOS Simulator / Web සඳහා 127.0.0.1 ද භාවිතා කරන්න
      final response = await http.post(
        Uri.parse('http://10.0.2.2:8000/agent/analyze-sentiment'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({
          'feedback_text': text,
        }),
      );

      if (!mounted) return;

      if (response.statusCode == 200) {
        final data = jsonDecode(response.body) as Map<String, dynamic>;

        // FastAPI Response එකෙහි snake_case (flagged_for_review) හෝ camelCase පරීක්ෂා කිරීම
        final isFlagged = data['flagged_for_review'] ?? data['flaggedForReview'] ?? false;

        if (isFlagged == true) {
          _showAlert(
            'Thank You!',
            'Your feedback will be reviewed by the admin team before being added to the system.',
          );
        } else {
          _showAlert('Thank You!', 'Your feedback has been submitted successfully.');
        }
        _feedbackController.clear();
      } else {
        _showAlert('Error', 'Failed to send feedback. Please try again.');
      }
    } catch (e) {
      if (!mounted) return;
      _showAlert('Error', 'Network connection error. Please try again.');
    } finally {
      if (mounted) {
        setState(() => _isLoading = false);
      }
    }
  }

  void _showAlert(String title, String message) {
    showDialog<void>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(title),
        content: Text(message),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(),
            child: const Text('OK'),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Service Feedback'),
      ),
      body: Padding(
        padding: const EdgeInsets.all(AppSpacing.lg),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            TextField(
              controller: _feedbackController,
              decoration: const InputDecoration(
                labelText: 'Enter your feedback',
                hintText: 'Share your thoughts here...',
                border: OutlineInputBorder(),
              ),
              maxLines: 4,
            ),
            const SizedBox(height: AppSpacing.lg),
            AppButton(
              text: 'Submit Feedback',
              isLoading: _isLoading,
              onPressed: _submitFeedback,
            ),
          ],
        ),
      ),
    );
  }
}