import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

import '../../../core/config/app_config.dart';
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
  int _selectedRating = 5;
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
      final response = await http.post(
        Uri.parse('${AppConfig.agentBaseUrl}/agent/analyze-sentiment'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({
          'job_id': widget.jobId,
          'rating': _selectedRating,
          'feedback_text': text,
        }),
      );

      if (!mounted) return;

      if (response.statusCode == 200) {
        final data = jsonDecode(response.body) as Map<String, dynamic>;
        final isFlagged = data['flagged_for_review'] ?? data['flaggedForReview'] ?? false;

        if (isFlagged == true) {
          _showAlert(
            'Thank You!',
            'Your feedback will be reviewed by the admin team before being added to the system.',
            shouldPop: true,
          );
        } else {
          _showAlert(
            'Thank You!',
            'Your feedback has been submitted successfully.',
            shouldPop: true,
          );
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

  void _showAlert(String title, String message, {bool shouldPop = false}) {
    showDialog<void>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(title),
        content: Text(message),
        actions: [
          TextButton(
            onPressed: () {
              Navigator.of(ctx).pop();
              if (shouldPop && mounted) {
                Navigator.of(context).pop();
              }
            },
            child: const Text('OK'),
          ),
        ],
      ),
    );
  }

  Widget _buildStarRating() {
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: List.generate(5, (index) {
        final starValue = index + 1;
        return IconButton(
          iconSize: 36,
          icon: Icon(
            starValue <= _selectedRating ? Icons.star : Icons.star_border,
            color: Colors.amber,
          ),
          onPressed: () {
            setState(() {
              _selectedRating = starValue;
            });
          },
        );
      }),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Service Feedback'),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(AppSpacing.lg),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Text(
              'How was your experience?',
              textAlign: TextAlign.center,
              style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: AppSpacing.md),
            _buildStarRating(),
            const SizedBox(height: AppSpacing.lg),
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