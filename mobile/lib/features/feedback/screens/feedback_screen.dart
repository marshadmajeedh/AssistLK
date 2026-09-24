import 'package:flutter/material.dart';

// ෆයිල් දෙකම එකම ෆෝල්ඩරයේ තියෙන නිසා කෙළින්ම නම විතරක් දුන්නා
import 'feedback_service.dart';

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
  final FeedbackService _feedbackService =
      FeedbackService(); // Service එක මෙතනට ගත්තා

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

    // කෙළින්ම HTTP call කරනවා වෙනුවට අපි Service එක පාවිච්චි කරනවා
    Map<String, dynamic>? result;
    String? errorMessage;
    try {
      result = await _feedbackService.submitFeedback(
        jobId: widget.jobId,
        customerId: '3fa85f64-5717-4562-b3fc-2c963f66afa6', // දැනට Test Customer ID එකක්
        rating: _selectedRating,
        comment: text,
      );
    } on FeedbackSubmissionException catch (e) {
      errorMessage = e.message;
    }

    if (!mounted) return;
    setState(() => _isLoading = false);

    if (errorMessage != null) {
      _showAlert('Error', errorMessage);
    } else if (result != null) {
      // Backend එකෙන් එන 'autoEscalatedToComplaint' අගය පරීක්ෂා කිරීම
      bool isEscalated = result['autoEscalatedToComplaint'] ?? false;

      if (isEscalated) {
        _showAlert(
          'Complaint Registered',
          result['message'] ??
              'Due to negative feedback, a complaint has been auto-created.',
          shouldPop: true,
        );
      } else {
        _showAlert(
          'Thank You!',
          result['message'] ?? 'Thank you for your valuable feedback!',
          shouldPop: true,
        );
      }
      _feedbackController.clear();
    } else {
      _showAlert('Error', 'Failed to send feedback. Please try again.');
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
              Navigator.of(ctx).pop(); // Alert එක close කිරීම
              if (shouldPop && mounted) {
                Navigator.of(context).pop(); // Screen එකෙන් back වීම
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
      appBar: AppBar(title: const Text('Service Feedback')),
      body: GestureDetector(
        onTap: () => FocusScope.of(context).unfocus(), // Keyboard එක hide කරන්න
        child: SingleChildScrollView(
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
      ),
    );
  }
}
