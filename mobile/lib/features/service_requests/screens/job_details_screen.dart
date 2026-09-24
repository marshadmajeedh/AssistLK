import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../providers/service_request_provider.dart';
import '../../../shared/theme/app_colors.dart';
import '../../../shared/theme/app_spacing.dart';
import '../../../shared/theme/app_text_styles.dart';
import '../../../shared/widgets/app_button.dart';

class JobDetailsScreen extends StatefulWidget {
  final String jobId;
  final String currentStatus;

  const JobDetailsScreen({
    super.key,
    required this.jobId,
    required this.currentStatus,
  });

  @override
  State<JobDetailsScreen> createState() => _JobDetailsScreenState();
}

class _JobDetailsScreenState extends State<JobDetailsScreen> {
  late String _status;
  bool _isUpdating = false;

  @override
  void initState() {
    super.initState();
    _status = widget.currentStatus;
  }

  Future<void> _updateJobStatus(String targetState) async {
    setState(() => _isUpdating = true);

    try {
      final result = await context
          .read<ServiceRequestProvider>()
          .validateTransition(
            jobId: widget.jobId,
            currentStatus: _status,
            targetStatus: targetState,
            elapsedMinutes: 0,
          );

      if (!mounted) return;

      if (result != null) {
        final isValid = result['is_valid'] ?? result['isValid'] ?? false;

        if (isValid == true) {
          if (targetState == 'COMPLETED') {
            final persisted = await context
                .read<ServiceRequestProvider>()
                .updateRequestStatusToCompleted(widget.jobId);
            if (!mounted) return;
            if (!persisted) {
              _showErrorDialog(
                context.read<ServiceRequestProvider>().error ??
                    'Failed to persist the completed status.',
              );
              return;
            }
          }

          setState(() => _status = targetState);
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
              content: Text('Status updated successfully to $targetState'),
              backgroundColor: AppColors.success,
            ),
          );
        } else {
          final message =
              result['reason'] ??
              result['message'] ??
              'Invalid status transition.';
          _showErrorDialog(message.toString());
        }
      } else {
        final error = context.read<ServiceRequestProvider>().error;
        _showErrorDialog(error ?? 'Failed to update status. Please try again.');
      }
    } finally {
      if (mounted) {
        setState(() => _isUpdating = false);
      }
    }
  }

  void _showErrorDialog(String message) {
    showDialog<void>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Error'),
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
      appBar: AppBar(title: Text('Job #${widget.jobId}')),
      body: Padding(
        padding: const EdgeInsets.all(AppSpacing.lg),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Current Status: $_status', style: AppTextStyles.cardHeading),
            const SizedBox(height: AppSpacing.xl),
            AppButton(
              text: 'Start Job',
              isLoading: _isUpdating,
              onPressed: () => _updateJobStatus('IN_PROGRESS'),
            ),
            const SizedBox(height: AppSpacing.md),
            OutlinedButton(
              onPressed: _isUpdating
                  ? null
                  : () => _updateJobStatus('COMPLETED'),
              style: OutlinedButton.styleFrom(
                minimumSize: const Size(double.infinity, 48),
              ),
              child: const Text('Complete Job'),
            ),
          ],
        ),
      ),
    );
  }
}
