import '../models/analysis_visual_evidence.dart';
import '../../auth/providers/auth_provider.dart';
import '../providers/problem_photos_controller.dart';
import '../services/problem_image_picker.dart';
import '../widgets/problem_photos.dart';
import '../widgets/request_summary_artwork.dart';
import '../models/location_source.dart';
import '../widgets/location_attribution.dart';
// FeedbackScreen එක සඳහා අලුත් import එක
import '../../feedback/screens/feedback_screen.dart';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../../shared/theme/app_colors.dart';
import '../../../../shared/theme/app_radius.dart';
import '../../../../shared/theme/app_spacing.dart';
import '../../../../shared/theme/app_text_styles.dart';
import '../../../../shared/widgets/app_button.dart';
import '../../../../shared/widgets/app_card.dart';
import '../models/canonical_service_category.dart';
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
  final ProblemImagePicker? imagePicker;

  const ServiceRequestDetailScreen({
    super.key,
    required this.requestId,
    this.imagePicker,
  });

  @override
  State<ServiceRequestDetailScreen> createState() =>
      _ServiceRequestDetailScreenState();
}

class _ServiceRequestDetailScreenState
    extends State<ServiceRequestDetailScreen> {
  late final ProblemPhotosController _photos;

  @override
  void initState() {
    super.initState();
    _photos = ProblemPhotosController(
      requestId: widget.requestId,
      service: context.read<ServiceRequestProvider>().serviceRequestService,
      picker:
          widget.imagePicker ??
          context.read<ProblemImagePicker?>() ??
          NativeProblemImagePicker(),
      auth: context.read<AuthProvider?>(),
    );
    _photos.addListener(_photoChanged);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      context.read<ServiceRequestProvider>().loadRequestById(widget.requestId);
      _loadPhotos();
    });
  }

  Future<void> _loadPhotos() async {
    await _photos.load();
    if (_photos.active) await _photos.recover();
  }

  void _photoChanged() {
    if (mounted) setState(() {});
  }

  @override
  void dispose() {
    _photos.removeListener(_photoChanged);
    _photos.dispose();
    super.dispose();
  }

  String _getAiClassificationText(ServiceRequestModel request) {
    switch (request.status) {
      case ServiceRequestStatus.created:
        return 'Not analyzed yet';
      case ServiceRequestStatus.analyzing:
        return 'Analysis in progress';
      case ServiceRequestStatus.awaitingInformation:
        return 'Needs more information';
      case ServiceRequestStatus.analyzed:
      case ServiceRequestStatus.readyForMatching:
      // Completed status එකේදිත් Category එක හරියටම පෙන්වීමට
      case ServiceRequestStatus.completed:
        return CanonicalServiceCategory.fromCanonicalOrDisplayName(
              request.category,
            )?.displayName ??
            (request.category.isEmpty ? 'Unclassified' : request.category);
      case ServiceRequestStatus.cancelled:
        if (request.category.isNotEmpty && request.category != 'Unclassified') {
          return CanonicalServiceCategory.fromCanonicalOrDisplayName(
                request.category,
              )?.displayName ??
              request.category;
        }
        return 'Not classified';
    }
  }

  Future<void> _triggerAnalysis(String id) async {
    if (!_photos.active ||
        _photos.busy ||
        _photos.picking ||
        _photos.hasPending) {
      return;
    }
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
    } else if (provider.error != null) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(provider.error!),
          backgroundColor: AppColors.error,
        ),
      );
    }
  }

  Future<void> _submitClarificationAnswers(
    String id,
    int round,
    Map<String, String> answers,
  ) async {
    if (!_photos.active ||
        _photos.busy ||
        _photos.picking ||
        _photos.hasPending) {
      return;
    }
    final provider = context.read<ServiceRequestProvider>();
    final result = await provider.submitClarificationAnswersAndReanalyze(
      id,
      round,
      answers,
    );

    if (!mounted) return;

    if (result != null) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            result.needsMoreInformation
                ? 'Answers submitted. Additional clarification is needed.'
                : 'Answers submitted and problem analyzed successfully!',
          ),
          backgroundColor: result.needsMoreInformation
              ? AppColors.warning
              : AppColors.success,
        ),
      );
    } else if (provider.error != null) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(provider.error!),
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
      final error = provider.error ?? 'Failed to mark ready for matching.';
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(error), backgroundColor: AppColors.error),
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
        SnackBar(content: Text(error), backgroundColor: AppColors.error),
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
    if (_photos.sessionEnded) {
      return const Scaffold(
        body: Center(
          child: Text('Your session has ended. Please sign in again.'),
        ),
      );
    }
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
          // Completed වුණාම Cancel button එක පෙන්වන්නේ නැති වෙන්න හැදුවා
          if (request.status != ServiceRequestStatus.cancelled &&
              request.status != ServiceRequestStatus.readyForMatching &&
              request.status != ServiceRequestStatus.completed &&
              request.status != ServiceRequestStatus.analyzing &&
              !provider.isAnalyzing &&
              !provider.analysisStateNeedsRefresh)
            IconButton(
              icon: const Icon(Icons.cancel_outlined, color: AppColors.error),
              tooltip: 'Cancel Request',
              onPressed: () => _cancelRequest(request.serviceRequestId),
            ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          await provider.loadRequestById(widget.requestId);
          await _photos.load();
        },
        child: SingleChildScrollView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.all(AppSpacing.lg),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // Header Card: Category, Urgency, Status, Service preference
              AppCard(
                child: RequestSummaryArtwork(
                  category: request.category,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Wrap(
                        alignment: WrapAlignment.spaceBetween,
                        crossAxisAlignment: WrapCrossAlignment.center,
                        spacing: AppSpacing.sm,
                        runSpacing: AppSpacing.sm,
                        children: [
                          Text(
                            request.category.isEmpty
                                ? 'Unclassified Request'
                                : (CanonicalServiceCategory.fromCanonicalOrDisplayName(
                                        request.category,
                                      )?.displayName ??
                                      request.category),
                            style: AppTextStyles.sectionHeading,
                          ),
                          StatusBadge(status: request.status),
                        ],
                      ),
                      if (request.status != ServiceRequestStatus.analyzed &&
                          request.status !=
                              ServiceRequestStatus.readyForMatching) ...[
                        const SizedBox(height: AppSpacing.xs + 2),
                        Wrap(
                          crossAxisAlignment: WrapCrossAlignment.center,
                          children: [
                            const Text(
                              'Service preference: ',
                              style: TextStyle(
                                fontSize: 12,
                                color: AppColors.textSecondary,
                                fontWeight: FontWeight.w500,
                              ),
                            ),
                            Text(
                              CanonicalServiceCategory.fromCanonicalOrDisplayName(
                                    request.categoryHint,
                                  )?.displayName ??
                                  (request.categoryHint == null
                                      ? 'Let AssistLK AI identify'
                                      : request.categoryHint!),
                              style: const TextStyle(
                                fontSize: 12,
                                fontWeight: FontWeight.w600,
                                color: AppColors.textPrimary,
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: AppSpacing.xs),
                        Wrap(
                          crossAxisAlignment: WrapCrossAlignment.center,
                          children: [
                            const Text(
                              'AssistLK AI classification: ',
                              style: TextStyle(
                                fontSize: 12,
                                color: AppColors.textSecondary,
                                fontWeight: FontWeight.w500,
                              ),
                            ),
                            Text(
                              _getAiClassificationText(request),
                              style: const TextStyle(
                                fontSize: 12,
                                fontWeight: FontWeight.w600,
                                color: AppColors.textPrimary,
                              ),
                            ),
                          ],
                        ),
                      ],
                      const SizedBox(height: AppSpacing.xs + 2),
                      Wrap(
                        crossAxisAlignment: WrapCrossAlignment.center,
                        spacing: AppSpacing.xs,
                        children: [
                          const Text(
                            'Urgency Level: ',
                            style: TextStyle(
                              fontSize: 12,
                              color: AppColors.textSecondary,
                            ),
                          ),
                          UrgencyChip(urgency: request.urgency),
                        ],
                      ),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: AppSpacing.sm),

              // Description Card
              AppCard(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text(
                      'Problem Description',
                      style: AppTextStyles.cardHeading,
                    ),
                    const SizedBox(height: AppSpacing.xs),
                    Text(request.description, style: AppTextStyles.body),
                    const SizedBox(height: AppSpacing.sm),
                    const Divider(color: AppColors.border, height: 1),
                    const SizedBox(height: AppSpacing.sm),

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
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                request.locationText.isEmpty
                                    ? 'No location specified'
                                    : request.locationText,
                                style: AppTextStyles.body.copyWith(
                                  fontWeight: FontWeight.w500,
                                ),
                              ),
                              if (request.locationSource ==
                                  LocationSource.openStreetMap)
                                const LocationAttribution(),
                              if (request.latitude != null &&
                                  request.longitude != null) ...[
                                const SizedBox(height: 2),
                                Row(
                                  children: [
                                    const Icon(
                                      Icons.my_location_rounded,
                                      size: 13,
                                      color: AppColors.success,
                                    ),
                                    const SizedBox(width: 4),
                                    Text(
                                      'GPS location captured',
                                      style: AppTextStyles.small.copyWith(
                                        color: AppColors.success,
                                        fontSize: 12,
                                        fontWeight: FontWeight.w500,
                                      ),
                                    ),
                                  ],
                                ),
                              ],
                            ],
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
              const SizedBox(height: AppSpacing.md),

              RequestProblemPhotos(
                controller: _photos,
                editable:
                    (request.status == ServiceRequestStatus.created ||
                        request.status ==
                            ServiceRequestStatus.awaitingInformation) &&
                    !provider.isAnalyzing &&
                    !provider.isLoading,
                onMutation: () async {
                  await provider.loadRequestById(widget.requestId);
                },
              ),
              const SizedBox(height: AppSpacing.md),
              // Dynamic Workflow Section based on status
              if (_photos.busy || _photos.picking || _photos.hasPending)
                const Text(
                  'Upload or discard selected photos before continuing with analysis.',
                )
              else ...[
                if ((request.status ==
                            ServiceRequestStatus.awaitingInformation ||
                        request.status == ServiceRequestStatus.cancelled) &&
                    request.latestAnalysis != null &&
                    request.latestAnalysis!.visualEvidence.status !=
                        AnalysisVisionStatus.notRequested) ...[
                  _persistedPhotoAnalysis(request),
                  const SizedBox(height: AppSpacing.md),
                ],
                _buildStatusWorkflowSection(
                  context,
                  request,
                  analysis,
                  provider,
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }

  Widget _persistedPhotoAnalysis(ServiceRequestModel request) {
    final latest = request.latestAnalysis!;
    return AnalysisResultCard(
      analysis: ProblemUnderstandingResultModel(
        workflowId: '',
        executionId: '',
        serviceRequestId: request.serviceRequestId,
        status: request.status,
        category: request.category,
        problemSummary: latest.detectedProblem,
        urgency: request.urgency,
        confidence: latest.confidence,
        needsMoreInformation:
            request.status == ServiceRequestStatus.awaitingInformation,
        followUpQuestions: const [],
      ),
      visualEvidence: latest.visualEvidence,
      hasPhotos: _photos.attachments.isNotEmpty,
      categoryHint: request.categoryHint,
      status: request.status,
    );
  }

  Widget _buildStatusWorkflowSection(
    BuildContext context,
    ServiceRequestModel request,
    ProblemUnderstandingResultModel? analysis,
    ServiceRequestProvider provider,
  ) {
    if (provider.analysisStateNeedsRefresh) {
      return AppCard(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Row(
              children: [
                Icon(
                  Icons.sync_problem_rounded,
                  color: AppColors.warning,
                  size: 24,
                ),
                SizedBox(width: AppSpacing.sm),
                Expanded(
                  child: Text(
                    'Unable to confirm the latest analysis status.',
                    style: AppTextStyles.cardHeading,
                  ),
                ),
              ],
            ),
            const SizedBox(height: AppSpacing.xs),
            Text(
              'A network interruption occurred after analysis was requested. Please refresh status to synchronize with the backend.',
              style: AppTextStyles.body.copyWith(
                color: AppColors.textSecondary,
              ),
            ),
            const SizedBox(height: AppSpacing.md),
            AppButton(
              text: 'Refresh Status',
              isLoading: provider.isLoading,
              onPressed: () =>
                  provider.loadRequestById(request.serviceRequestId),
            ),
          ],
        ),
      );
    }

    if (provider.isAnalyzing) {
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
            Flexible(
              child: Text(
                'AssistLK AI analysis in progress',
                style: AppTextStyles.body,
              ),
            ),
          ],
        ),
      );
    }

    if (request.status == ServiceRequestStatus.analyzing) {
      return AppCard(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Row(
              children: [
                SizedBox(
                  width: 20,
                  height: 20,
                  child: CircularProgressIndicator(strokeWidth: 2),
                ),
                SizedBox(width: AppSpacing.md),
                Expanded(
                  child: Text(
                    'AssistLK AI analysis in progress',
                    style: AppTextStyles.cardHeading,
                  ),
                ),
              ],
            ),
            const SizedBox(height: AppSpacing.xs),
            Text(
              'Analysis is taking longer than expected. Your request is still being processed.',
              style: AppTextStyles.body.copyWith(
                color: AppColors.textSecondary,
              ),
            ),
            const SizedBox(height: AppSpacing.md),
            AppButton(
              text: 'Refresh Status',
              isLoading: provider.isLoading,
              onPressed: () =>
                  provider.loadRequestById(request.serviceRequestId),
            ),
          ],
        ),
      );
    }

    switch (request.status) {
      case ServiceRequestStatus.created:
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
              'Next Step: AssistLK AI Analysis',
              style: AppTextStyles.cardHeading,
            ),
            const SizedBox(height: AppSpacing.xs),
            Text(
              'Use AssistLK AI to identify the service category, estimate urgency, and check whether more information is needed.',
              style: AppTextStyles.body.copyWith(
                color: AppColors.textSecondary,
              ),
            ),
            const SizedBox(height: AppSpacing.md),
            AppButton(
              text: 'Analyze with AssistLK AI',
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
              Flexible(
                child: Text(
                  'AssistLK AI analysis in progress',
                  style: AppTextStyles.body,
                ),
              ),
            ],
          ),
        );

      case ServiceRequestStatus.awaitingInformation:
        return ClarificationSection(
          followUpQuestions: analysis?.followUpQuestions ?? const [],
          clarifications: request.clarifications,
          hasReachedMaxRounds: request.hasCompletedFinalClarificationAnalysis,
          isReanalyzing: provider.isAnalyzing,
          isSubmitting: provider.isLoading,
          onEditDetails: () => _navigateToEdit(request),
          onReanalyze: () => _triggerAnalysis(request.serviceRequestId),
          onSubmitAnswers: (round, answers) => _submitClarificationAnswers(
            request.serviceRequestId,
            round,
            answers,
          ),
        );

      case ServiceRequestStatus.analyzed:
        final displayAnalysis =
            analysis ??
            (request.latestAnalysis != null
                ? ProblemUnderstandingResultModel(
                    workflowId: '',
                    executionId: '',
                    serviceRequestId: request.serviceRequestId,
                    status: request.status,
                    category: request.category,
                    problemSummary: request.latestAnalysis!.detectedProblem,
                    urgency: request.urgency,
                    confidence: request.latestAnalysis!.confidence,
                    needsMoreInformation: false,
                    followUpQuestions: const [],
                  )
                : ProblemUnderstandingResultModel(
                    workflowId: '',
                    executionId: '',
                    serviceRequestId: request.serviceRequestId,
                    status: request.status,
                    category: request.category,
                    problemSummary: request.description,
                    urgency: request.urgency,
                    confidence: 0.0,
                    needsMoreInformation: false,
                    followUpQuestions: const [],
                  ));

        return Column(
          children: [
            AnalysisResultCard(
              analysis: displayAnalysis,
              visualEvidence:
                  request.latestAnalysis?.visualEvidence ??
                  const AnalysisVisualEvidence(),
              hasPhotos: _photos.attachments.isNotEmpty,
              categoryHint: request.categoryHint,
              status: request.status,
            ),
            const SizedBox(height: AppSpacing.md),
            AppButton(
              text: 'Mark Ready for Matching',
              isLoading: provider.isLoading,
              onPressed: () => _markReadyForMatching(request.serviceRequestId),
            ),
          ],
        );

      case ServiceRequestStatus.readyForMatching:
        final readyDisplayAnalysis =
            analysis ??
            (request.latestAnalysis != null
                ? ProblemUnderstandingResultModel(
                    workflowId: '',
                    executionId: '',
                    serviceRequestId: request.serviceRequestId,
                    status: request.status,
                    category: request.category,
                    problemSummary: request.latestAnalysis!.detectedProblem,
                    urgency: request.urgency,
                    confidence: request.latestAnalysis!.confidence,
                    needsMoreInformation: false,
                    followUpQuestions: const [],
                  )
                : ProblemUnderstandingResultModel(
                    workflowId: '',
                    executionId: '',
                    serviceRequestId: request.serviceRequestId,
                    status: request.status,
                    category: request.category,
                    problemSummary: request.description,
                    urgency: request.urgency,
                    confidence: 0.0,
                    needsMoreInformation: false,
                    followUpQuestions: const [],
                  ));

        return Column(
          children: [
            AnalysisResultCard(
              analysis: readyDisplayAnalysis,
              visualEvidence:
                  request.latestAnalysis?.visualEvidence ??
                  const AnalysisVisualEvidence(),
              hasPhotos: _photos.attachments.isNotEmpty,
              categoryHint: request.categoryHint,
              status: request.status,
            ),
            const SizedBox(height: AppSpacing.md),
            const ReadyForMatchingSection(),
          ],
        );

      // Job Completed UI සහ Feedback Button එක
      case ServiceRequestStatus.completed:
        return AppCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Row(
                children: [
                  Icon(
                    Icons.check_circle_rounded,
                    color: AppColors.success,
                    size: 24,
                  ),
                  SizedBox(width: AppSpacing.sm),
                  Expanded(
                    child: Text(
                      'Job Completed',
                      style: AppTextStyles.cardHeading,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: AppSpacing.xs),
              Text(
                'Your service request has been successfully completed. We would love to hear your thoughts!',
                style: AppTextStyles.body.copyWith(
                  color: AppColors.textSecondary,
                ),
              ),
              const SizedBox(height: AppSpacing.md),
              AppButton(
                text: 'Provide Feedback',
                onPressed: () {
                  final serviceJobId = request.serviceJobId;
                  if (serviceJobId == null || serviceJobId.isEmpty) {
                    ScaffoldMessenger.of(context).showSnackBar(
                      const SnackBar(
                        content: Text(
                          'Feedback is unavailable until a service job is assigned.',
                        ),
                      ),
                    );
                    return;
                  }

                  Navigator.of(context).push(
                    MaterialPageRoute(
                      builder: (_) => FeedbackScreen(jobId: serviceJobId),
                    ),
                  );
                },
              ),
            ],
          ),
        );

      case ServiceRequestStatus.cancelled:
        return AppCard(
          child: Padding(
            padding: const EdgeInsets.symmetric(vertical: AppSpacing.md),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                const Icon(Icons.cancel_outlined, color: AppColors.error),
                const SizedBox(width: AppSpacing.sm),
                Text(
                  'This request has been cancelled.',
                  style: AppTextStyles.body.copyWith(
                    color: AppColors.textSecondary,
                  ),
                ),
              ],
            ),
          ),
        );

      default:
        return const SizedBox.shrink();
    }
  }
}
