import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../../shared/theme/app_colors.dart';
import '../../../../shared/theme/app_radius.dart';
import '../../../../shared/theme/app_spacing.dart';
import '../../../../shared/theme/app_text_styles.dart';
import '../../../../shared/widgets/app_button.dart';
import '../../../../shared/widgets/app_card.dart';
import '../models/problem_understanding_result_model.dart';
import '../models/service_request_model.dart';
import '../models/service_request_status.dart';
import '../providers/service_request_provider.dart';
import '../widgets/analysis_result_card.dart';
import '../widgets/clarification_section.dart';
import '../widgets/ready_for_matching_section.dart';
import '../widgets/status_badge.dart';
import '../widgets/urgency_chip.dart';
import 'edit_service_request_screen.dart';

class ServiceRequestDetailScreen extends StatefulWidget {
  final String requestId;

  const ServiceRequestDetailScreen({
    super.key,
    required this.requestId,
  });

  @override
  State<ServiceRequestDetailScreen> createState() =>
      _ServiceRequestDetailScreenState();
}

class _ServiceRequestDetailScreenState
    extends State<ServiceRequestDetailScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<ServiceRequestProvider>().loadRequestById(widget.requestId);
    });
  }

  Future<void> _triggerAnalysis(String id) async {
    final provider = context.read<ServiceRequestProvider>();
    final result = await provider.analyzeRequest(id);

    if (!mounted) return;

    if (result != null) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            result.needsMoreInformation
                ? 'Analysis complete: Additional clarification is needed.'
                : 'Problem analyzed successfully!',
          ),
          backgroundColor: result.needsMoreInformation
              ? AppColors.warning
              : AppColors.success,
        ),
      );
    } else {
      final error = provider.error ?? 'Analysis failed. Please try again.';
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(error),
          backgroundColor: AppColors.error,
        ),
      );
    }
  }

  Future<void> _markReadyForMatching(String id) async {
    final provider = context.read<ServiceRequestProvider>();
    final result = await provider.markReadyForMatching(id);

    if (!mounted) return;

    if (result != null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Request is now marked Ready for Matching!'),
          backgroundColor: AppColors.success,
        ),
      );
    } else {
      final error =
          provider.error ?? 'Failed to mark ready for matching.';
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(error),
          backgroundColor: AppColors.error,
        ),
      );
    }
  }

  Future<void> _cancelRequest(String id) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogCtx) => AlertDialog(
        title: const Text('Cancel Service Request'),
        content: const Text(
          'Are you sure you want to cancel this service request? This action cannot be undone.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogCtx).pop(false),
            child: const Text('Keep Request'),
          ),
          TextButton(
            onPressed: () => Navigator.of(dialogCtx).pop(true),
            style: TextButton.styleFrom(foregroundColor: AppColors.error),
            child: const Text('Cancel Request'),
          ),
        ],
      ),
    );

    if (confirmed != true || !mounted) return;

    final provider = context.read<ServiceRequestProvider>();
    final cancelled = await provider.cancelRequest(id);

    if (!mounted) return;

    if (cancelled != null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Service request cancelled.'),
          backgroundColor: AppColors.textSecondary,
        ),
      );
    } else {
      final error = provider.error ?? 'Failed to cancel request.';
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(error),
          backgroundColor: AppColors.error,
        ),
      );
    }
  }

  void _navigateToEdit(ServiceRequestModel request) {
    Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => EditServiceRequestScreen(request: request),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<ServiceRequestProvider>();
    final request = provider.currentRequest;
    final analysis = provider.currentAnalysis;

    if (provider.isLoading && request == null) {
      return Scaffold(
        appBar: AppBar(title: const Text('Request Details')),
        body: const Center(child: CircularProgressIndicator()),
      );
    }

    if (request == null) {
      return Scaffold(
        appBar: AppBar(title: const Text('Request Details')),
        body: Center(
          child: Padding(
            padding: const EdgeInsets.all(AppSpacing.lg),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                const Icon(
                  Icons.error_outline_rounded,
                  size: 48,
                  color: AppColors.error,
                ),
                const SizedBox(height: AppSpacing.md),
                Text(
                  provider.error ?? 'Service request not found.',
                  style: AppTextStyles.body,
                  textAlign: TextAlign.center,
                ),
                const SizedBox(height: AppSpacing.lg),
                AppButton(
                  text: 'Back to Home',
                  onPressed: () => Navigator.of(context).pop(),
                ),
              ],
            ),
          ),
        ),
      );
    }

    return Scaffold(
      appBar: AppBar(
        title: const Text('Request Details'),
        actions: [
          if (request.status != ServiceRequestStatus.cancelled &&
              request.status != ServiceRequestStatus.readyForMatching)
            IconButton(
              icon: const Icon(Icons.cancel_outlined, color: AppColors.error),
              tooltip: 'Cancel Request',
              onPressed: () => _cancelRequest(request.serviceRequestId),
            ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () => provider.loadRequestById(widget.requestId),
        child: SingleChildScrollView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.all(AppSpacing.lg),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // Header Card: Category, Urgency, Status
              AppCard(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Expanded(
                          child: Text(
                            request.category.isEmpty
                                ? 'Unclassified Request'
                                : request.category,
                            style: AppTextStyles.sectionHeading,
                          ),
                        ),
                        const SizedBox(width: AppSpacing.sm),
                        StatusBadge(status: request.status),
                      ],
                    ),
                    const SizedBox(height: AppSpacing.md),
                    Row(
                      children: [
                        const Text(
                          'Urgency Level: ',
                          style: TextStyle(
                            fontSize: 13,
                            color: AppColors.textSecondary,
                          ),
                        ),
                        UrgencyChip(urgency: request.urgency),
                      ],
                    ),
                  ],
                ),
              ),
              const SizedBox(height: AppSpacing.md),

              // Description Card
              AppCard(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text(
                      'Problem Description',
                      style: AppTextStyles.cardHeading,
                    ),
                    const SizedBox(height: AppSpacing.sm),
                    Text(
                      request.description,
                      style: AppTextStyles.body,
                    ),
                    const SizedBox(height: AppSpacing.md),
                    const Divider(color: AppColors.border, height: 1),
                    const SizedBox(height: AppSpacing.md),

                    // Location
                    Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Icon(
                          Icons.location_on_outlined,
                          size: 18,
                          color: AppColors.primary,
                        ),
                        const SizedBox(width: AppSpacing.xs),
                        Expanded(
                          child: Text(
                            request.locationText.isEmpty
                                ? 'No location specified'
                                : request.locationText,
                            style: AppTextStyles.body.copyWith(
                              fontWeight: FontWeight.w500,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
              const SizedBox(height: AppSpacing.lg),

              // Dynamic Workflow Section based on status
              _buildStatusWorkflowSection(context, request, analysis, provider),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildStatusWorkflowSection(
    BuildContext context,
    ServiceRequestModel request,
    ProblemUnderstandingResultModel? analysis,
    ServiceRequestProvider provider,
  ) {
    switch (request.status) {
      case ServiceRequestStatus.created:
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
              'Next Step: AI Understanding',
              style: AppTextStyles.cardHeading,
            ),
            const SizedBox(height: AppSpacing.xs),
            Text(
              'Trigger Gemini AI to categorize the issue, determine urgency, and verify if further information is required.',
              style: AppTextStyles.body.copyWith(
                color: AppColors.textSecondary,
              ),
            ),
            const SizedBox(height: AppSpacing.md),
            AppButton(
              text: 'Analyze with Gemini AI',
              isLoading: provider.isAnalyzing,
              onPressed: () => _triggerAnalysis(request.serviceRequestId),
            ),
          ],
        );

      case ServiceRequestStatus.analyzing:
        return const AppCard(
          child: Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              SizedBox(
                width: 20,
                height: 20,
                child: CircularProgressIndicator(strokeWidth: 2),
              ),
              SizedBox(width: AppSpacing.md),
              Text(
                'Gemini AI is analyzing your request...',
                style: AppTextStyles.body,
              ),
            ],
          ),
        );

      case ServiceRequestStatus.awaitingInformation:
        // Backend currently does not persist followUpQuestions in the database.
        // Stored temporarily in memory until customer completes clarification.
        final questions = analysis != null && analysis.followUpQuestions.isNotEmpty
            ? analysis.followUpQuestions
            : <String>[
                'Please provide further details regarding the issue.',
              ];

        return ClarificationSection(
          followUpQuestions: questions,
          isReanalyzing: provider.isAnalyzing,
          onEditDetails: () => _navigateToEdit(request),
          onReanalyze: () => _triggerAnalysis(request.serviceRequestId),
        );

      case ServiceRequestStatus.analyzed:
        final displayAnalysis = analysis ??
            ProblemUnderstandingResultModel(
              workflowId: '',
              executionId: '',
              serviceRequestId: request.serviceRequestId,
              status: request.status,
              category: request.category,
              problemSummary: request.description,
              urgency: request.urgency,
              confidence: 0.90,
              needsMoreInformation: false,
              followUpQuestions: const [],
            );

        return Column(
          children: [
            AnalysisResultCard(analysis: displayAnalysis),
            const SizedBox(height: AppSpacing.md),
            AppButton(
              text: 'Mark Ready for Matching',
              isLoading: provider.isLoading,
              onPressed: () => _markReadyForMatching(request.serviceRequestId),
            ),
          ],
        );

      case ServiceRequestStatus.readyForMatching:
        return const ReadyForMatchingSection();

      case ServiceRequestStatus.cancelled:
        return Container(
          width: double.infinity,
          padding: const EdgeInsets.all(AppSpacing.md),
          decoration: BoxDecoration(
            color: const Color(0xFFFEF2F2),
            borderRadius: BorderRadius.circular(AppRadius.medium),
            border: Border.all(color: const Color(0xFFFECACA)),
          ),
          child: const Row(
            children: [
              Icon(Icons.cancel_rounded, color: AppColors.error),
              SizedBox(width: AppSpacing.sm),
              Expanded(
                child: Text(
                  'This service request has been cancelled.',
                  style: TextStyle(
                    fontSize: 14,
                    color: AppColors.error,
                    fontWeight: FontWeight.w500,
                  ),
                ),
              ),
            ],
          ),
        );
    }
  }
}
