import 'dart:async';

import 'package:flutter/material.dart';
import 'package:latlong2/latlong.dart';

import '../../core/api/api_client.dart';
import '../../core/config/app_config.dart';
import '../../shared/theme/app_colors.dart';
import '../feedback/screens/feedback_service.dart';
import 'widgets/live_tracking_map.dart';

class CustomerJobTrackingScreen extends StatefulWidget {
  final String jobId;
  final String status;
  final double destinationLatitude;
  final double destinationLongitude;
  final String? providerName;
  final String? providerImageUrl;
  final String? providerCategory;
  final double? providerRating;
  final String? providerPhone;
  final String? completionImageUrl;
  final String? completionSummary;
  final double? additionalCost;
  final bool hasFeedback;
  final int? feedbackRating;
  final String? feedbackComment;

  const CustomerJobTrackingScreen({
    super.key,
    required this.jobId,
    this.status = 'Assigned',
    required this.destinationLatitude,
    required this.destinationLongitude,
    this.providerName,
    this.providerImageUrl,
    this.providerCategory,
    this.providerRating,
    this.providerPhone,
    this.completionImageUrl,
    this.completionSummary,
    this.additionalCost,
    this.hasFeedback = false,
    this.feedbackRating,
    this.feedbackComment,
  });

  @override
  State<CustomerJobTrackingScreen> createState() =>
      _CustomerJobTrackingScreenState();
}

class _CustomerJobTrackingScreenState extends State<CustomerJobTrackingScreen> {
  static const _lifecycleStages = [
    'Assigned',
    'OnTheWay',
    'Arrived',
    'InProgress',
    'Completed',
  ];

  String? _connectionError;
  bool _trackingStopped = false;
  late String _currentStatus;
  late final TextEditingController _feedbackController;
  late final FeedbackService _feedbackService;
  late int _selectedRating;
  bool _isSubmittingFeedback = false;
  bool _feedbackSubmitted = false;
  Timer? _statusPollTimer;
  bool _isPolling = false;
  String? _completionImageUrl;
  String? _completionSummary;
  // ignore: unused_field
  double? _additionalCost;
  int? _feedbackRating;
  String? _feedbackComment;

  @override
  void initState() {
    super.initState();
    _currentStatus = _normalizeStatus(widget.status);
    _feedbackController = TextEditingController(
      text: widget.feedbackComment ?? '',
    );
    _feedbackService = FeedbackService();
    _selectedRating = widget.feedbackRating ?? 5;
    _feedbackSubmitted = widget.hasFeedback;
    _completionImageUrl = widget.completionImageUrl;
    _completionSummary = widget.completionSummary;
    _additionalCost = widget.additionalCost;
    _feedbackRating = widget.feedbackRating;
    _feedbackComment = widget.feedbackComment;
    _startStatusPolling();
  }

  @override
  void didUpdateWidget(covariant CustomerJobTrackingScreen oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.status != widget.status) {
      setState(() {
        _currentStatus = _normalizeStatus(widget.status);
        if (_currentStatus != 'OnTheWay') _trackingStopped = true;
        _completionImageUrl = widget.completionImageUrl;
        _completionSummary = widget.completionSummary;
        _additionalCost = widget.additionalCost;
        _feedbackRating = widget.feedbackRating;
        _feedbackComment = widget.feedbackComment;
      });
    }
  }

  @override
  void dispose() {
    _statusPollTimer?.cancel();
    _feedbackController.dispose();
    super.dispose();
  }

  void _startStatusPolling() {
    unawaited(_pollLatestJob());
    _statusPollTimer = Timer.periodic(
      const Duration(seconds: 4),
      (_) => unawaited(_pollLatestJob()),
    );
  }

  Future<void> _pollLatestJob() async {
    if (_isPolling) return;
    _isPolling = true;

    try {
      final response = await ApiClient().client.get('/service-requests/my');
      final payload = response.data;
      if (payload is! List) return;

      Map<String, dynamic>? matchingRequest;
      for (final item in payload) {
        if (item is Map && item['serviceJobId']?.toString() == widget.jobId) {
          matchingRequest = Map<String, dynamic>.from(item);
          break;
        }
      }

      if (matchingRequest == null || !mounted) return;

      final polledStatus = _normalizeStatus(
        matchingRequest['jobStatus']?.toString() ??
            matchingRequest['status']?.toString() ??
            _currentStatus,
      );
      final completion = matchingRequest['completionRecord'];
      final completionData = completion is Map
          ? Map<String, dynamic>.from(completion)
          : null;
      final polledImageUrl = _readProofImageUrl(
        matchingRequest,
        completionData,
      );
      final polledSummary =
          (completionData?['summaryNotes'] ?? completionData?['workSummary'])
              ?.toString();
      final polledAdditionalCost = (completionData?['additionalCost'] as num?)
          ?.toDouble();
      final polledFeedbackComment = matchingRequest['feedbackComment']
          ?.toString();
      final polledFeedbackRating = (matchingRequest['feedbackRating'] as num?)
          ?.toInt();
      final statusIndex = _lifecycleStages.indexOf(polledStatus);

      setState(() {
        if (statusIndex > _currentStageIndex) {
          _currentStatus = polledStatus;
        }
        if (polledStatus == 'Completed' || completionData != null) {
          _currentStatus = 'Completed';
          _trackingStopped = true;
        }
        if (polledImageUrl != null && polledImageUrl.isNotEmpty) {
          _completionImageUrl = polledImageUrl;
        }
        if (polledSummary != null && polledSummary.isNotEmpty) {
          _completionSummary = polledSummary;
        }
        if (polledAdditionalCost != null) {
          _additionalCost = polledAdditionalCost;
        }
        _feedbackSubmitted = matchingRequest?['hasFeedback'] == true;
        _feedbackRating = polledFeedbackRating ?? _feedbackRating;
        _feedbackComment = polledFeedbackComment ?? _feedbackComment;
        if (_feedbackComment != null && _feedbackController.text.isEmpty) {
          _feedbackController.text = _feedbackComment!;
        }
        if (_feedbackRating != null) _selectedRating = _feedbackRating!;
      });
    } catch (_) {
      // A temporary polling failure should not interrupt the tracking screen.
    } finally {
      _isPolling = false;
    }
  }

  String? _readProofImageUrl(
    Map<String, dynamic> jobPayload,
    Map<String, dynamic>? completionData,
  ) {
    final rawUrl =
        completionData?['proofOfWorkImageUrl'] ??
        completionData?['ProofOfWorkImageUrl'] ??
        jobPayload['proofOfWorkImageUrl'] ??
        jobPayload['ProofOfWorkImageUrl'];
    final value = rawUrl?.toString().trim();
    if (value == null || value.isEmpty) return null;

    final uri = Uri.tryParse(value);
    if (uri == null || uri.hasScheme) return value;

    final apiUri = Uri.tryParse(AppConfig.apiBaseUrl);
    return apiUri?.resolve(value).toString() ?? value;
  }

  String _resolveImageUrl(String value) {
    final uri = Uri.tryParse(value.trim());
    if (uri == null || uri.hasScheme) return value;

    final apiUri = Uri.tryParse(AppConfig.apiBaseUrl);
    return apiUri?.resolve(value).toString() ?? value;
  }

  String _normalizeStatus(String status) {
    switch (status.trim().toLowerCase()) {
      case 'on the way':
      case 'ontheway':
      case 'providerontheway':
        return 'OnTheWay';
      case 'arrived':
      case 'providerarrived':
        return 'Arrived';
      case 'in progress':
      case 'inprogress':
      case 'workstarted':
        return 'InProgress';
      case 'completed':
      case 'complete':
      case 'workcompleted':
      case 'jobcompleted':
      case 'job completed':
      case 'completed successfully':
      case 'job completed successfully':
      case 'servicejobstatus.completed':
        return 'Completed';
      case 'assigned':
      default:
        return 'Assigned';
    }
  }

  int get _currentStageIndex {
    if (_currentStatus == 'Completed') {
      return _lifecycleStages.length - 1;
    }
    final index = _lifecycleStages.indexOf(_currentStatus);
    return index < 0 ? 0 : index;
  }

  Future<void> _submitFeedback() async {
    final comment = _feedbackController.text.trim();
    if (comment.isEmpty) {
      _showMessage('Please enter a review before submitting.');
      return;
    }

    setState(() => _isSubmittingFeedback = true);
    try {
      await _feedbackService.submitFeedback(
        jobId: widget.jobId,
        rating: _selectedRating,
        comment: comment,
      );
      if (!mounted) return;
      setState(() {
        _feedbackSubmitted = true;
        _isSubmittingFeedback = false;
        _feedbackComment = comment;
        _feedbackRating = _selectedRating;
      });
      _showMessage('Thank you for your feedback.');
    } on FeedbackSubmissionException catch (error) {
      if (!mounted) return;
      setState(() => _isSubmittingFeedback = false);
      _showMessage(error.message);
    } catch (error) {
      if (!mounted) return;
      setState(() => _isSubmittingFeedback = false);
      _showMessage('Feedback submission failed: $error');
    }
  }

  void _showMessage(String message) {
    ScaffoldMessenger.of(context)
        .showSnackBar(SnackBar(content: Text(message)));
  }

  Widget _buildLifecycleBar() {
    return Container(
      margin: const EdgeInsets.fromLTRB(16, 16, 16, 8),
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: AppColors.border.withValues(alpha: 0.7)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Service Status',
            style: TextStyle(
              color: AppColors.textPrimary,
              fontSize: 16,
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: 12),
          ...List.generate(_lifecycleStages.length, (index) {
            final isComplete = _currentStatus == 'Completed' ||
                index < _currentStageIndex;
            final isCurrent = _currentStatus != 'Completed' &&
                index == _currentStageIndex;
            final isLast = index == _lifecycleStages.length - 1;

            return Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                SizedBox(
                  width: 32,
                  child: Column(
                    children: [
                      Container(
                        key: ValueKey(
                          'customer_tracking_stage_${_lifecycleStages[index]}',
                        ),
                        width: 26,
                        height: 26,
                        decoration: BoxDecoration(
                          color: isComplete || isCurrent
                              ? AppColors.primary
                              : AppColors.surface,
                          shape: BoxShape.circle,
                          border: isComplete || isCurrent
                              ? null
                              : Border.all(color: AppColors.disabled, width: 2),
                        ),
                        child: isComplete
                            ? const Icon(Icons.check, color: Colors.white, size: 17)
                            : isCurrent
                                ? const Icon(
                                    Icons.radio_button_checked,
                                    color: Colors.white,
                                    size: 14,
                                  )
                                : null,
                      ),
                      if (!isLast)
                        Container(
                          width: 2,
                          height: 28,
                          color: index < _currentStageIndex
                              ? AppColors.primary
                              : AppColors.border,
                        ),
                    ],
                  ),
                ),
                Expanded(
                  child: AnimatedContainer(
                    duration: const Duration(milliseconds: 180),
                    margin: const EdgeInsets.only(bottom: 6),
                    padding: const EdgeInsets.symmetric(
                      horizontal: 12,
                      vertical: 8,
                    ),
                    decoration: BoxDecoration(
                      color: isCurrent ? AppColors.primarySurface : null,
                      borderRadius: BorderRadius.circular(10),
                    ),
                    child: Text(
                      _lifecycleStages[index],
                      style: TextStyle(
                        color: isComplete || isCurrent
                            ? AppColors.primary
                            : AppColors.textSecondary,
                        fontSize: 14,
                        fontWeight: isComplete || isCurrent
                            ? FontWeight.w700
                            : FontWeight.w500,
                      ),
                    ),
                  ),
                ),
              ],
            );
          }),
        ],
      ),
    );
  }

  Widget _buildProviderCard() {
    final name = widget.providerName?.trim().isNotEmpty == true
        ? widget.providerName!
      : 'Assigned Agent';
    final category = widget.providerCategory?.trim().isNotEmpty == true
        ? widget.providerCategory!
      : 'Service Agent';
    final rating = widget.providerRating?.toStringAsFixed(1) ?? 'Not rated';

    return Card(
      margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          children: [
            CircleAvatar(
              radius: 28,
              backgroundImage: widget.providerImageUrl?.isNotEmpty == true
                  ? NetworkImage(widget.providerImageUrl!)
                  : null,
              child: widget.providerImageUrl?.isNotEmpty == true
                  ? null
                  : const Icon(Icons.person, color: AppColors.primary),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    name,
                    style: const TextStyle(
                      color: AppColors.textPrimary,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  Text(
                    category,
                    style: const TextStyle(color: AppColors.textSecondary),
                  ),
                  Row(
                    children: [
                      if (rating != 'Not rated') ...[
                        const Icon(
                          Icons.star_rounded,
                          color: AppColors.primary,
                          size: 18,
                        ),
                        const SizedBox(width: 3),
                      ],
                      Text(
                        rating,
                        style: const TextStyle(color: AppColors.primary),
                      ),
                    ],
                  ),
                  if (widget.providerPhone?.isNotEmpty == true)
                    Text(
                      widget.providerPhone!,
                      style: const TextStyle(color: AppColors.textSecondary),
                    ),
                ],
              ),
            ),
            if (widget.providerPhone?.isNotEmpty == true)
              IconButton(
                tooltip: 'Contact agent',
                icon: const Icon(Icons.phone, color: AppColors.primary),
                onPressed: () => _showMessage(widget.providerPhone!),
              ),
          ],
        ),
      ),
    );
  }

  Widget _buildCompletionSummary() {
    return Card(
      margin: const EdgeInsets.all(16),
      color: AppColors.primarySurface,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                const CircleAvatar(
                  key: ValueKey('completion_status_icon'),
                  radius: 20,
                  backgroundColor: AppColors.primary,
                  child: Icon(Icons.check, color: Colors.white),
                ),
                const SizedBox(width: 12),
                const Expanded(
                  child: Text(
                    'Thank You! Job Completed Successfully',
                    style: TextStyle(
                      color: AppColors.primaryDark,
                      fontSize: 17,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
              ],
            ),
            if (_completionSummary?.isNotEmpty == true) ...[
              const SizedBox(height: 12),
              Text(
                'Summary: $_completionSummary',
                style: const TextStyle(color: AppColors.textPrimary),
              ),
            ],
          ],
        ),
      ),
    );
  }

  Widget _buildServiceProofSection() {
    final hasProofImage = _completionImageUrl?.isNotEmpty == true;
    return Card(
      margin: const EdgeInsets.fromLTRB(16, 0, 16, 16),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
              'Service Proof Image',
              style: TextStyle(
                color: AppColors.textPrimary,
                fontSize: 16,
                fontWeight: FontWeight.w700,
              ),
            ),
            const SizedBox(height: 12),
            if (hasProofImage)
              ClipRRect(
                borderRadius: BorderRadius.circular(12),
                child: Image.network(
                  _resolveImageUrl(_completionImageUrl!),
                  width: double.infinity,
                  height: 190,
                  fit: BoxFit.cover,
                  errorBuilder: (_, error, stackTrace) => _proofImageFallback(),
                ),
              )
            else
              _proofImageFallback(),
            const SizedBox(height: 10),
            const Row(
              children: [
                Icon(Icons.photo_outlined, color: AppColors.primary, size: 18),
                SizedBox(width: 6),
                Text(
                  'Proof photo',
                  style: TextStyle(
                    color: AppColors.primary,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 4),
            Text(
              hasProofImage
                  ? 'Photo shared by your agent as proof of completed work.'
                  : 'No proof photo was provided for this service.',
              style: const TextStyle(
                color: AppColors.textSecondary,
                fontSize: 12,
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _proofImageFallback() {
    return Container(
      width: double.infinity,
      height: 150,
      decoration: BoxDecoration(
        color: AppColors.primarySurface,
        borderRadius: BorderRadius.circular(12),
      ),
      child: const Icon(
        Icons.image_outlined,
        color: AppColors.primary,
        size: 42,
      ),
    );
  }

  Widget _buildFeedbackSection() {
    if (_feedbackSubmitted) {
      return const Card(
        margin: EdgeInsets.fromLTRB(16, 0, 16, 16),
        child: Padding(
          padding: EdgeInsets.all(16),
          child: Text('Feedback submitted. Thank you.'),
        ),
      );
    }

    return Card(
      margin: const EdgeInsets.fromLTRB(16, 0, 16, 16),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Text(
              'How was your experience?',
              style: TextStyle(
                color: AppColors.textPrimary,
                fontWeight: FontWeight.bold,
              ),
            ),
            Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: List.generate(5, (index) {
                final rating = index + 1;
                return IconButton(
                  icon: Icon(
                    rating <= _selectedRating ? Icons.star : Icons.star_border,
                    color: AppColors.primary,
                  ),
                  onPressed: _isSubmittingFeedback
                      ? null
                      : () => setState(() => _selectedRating = rating),
                );
              }),
            ),
            TextField(
              controller: _feedbackController,
              maxLines: 3,
              enabled: !_isSubmittingFeedback,
              decoration: const InputDecoration(
                labelText: 'Review',
                border: OutlineInputBorder(),
                focusedBorder: OutlineInputBorder(
                  borderSide: BorderSide(color: AppColors.primary, width: 2),
                ),
              ),
            ),
            const SizedBox(height: 12),
            ElevatedButton(
              onPressed: _isSubmittingFeedback ? null : _submitFeedback,
              style: ElevatedButton.styleFrom(
                backgroundColor: AppColors.primary,
                foregroundColor: AppColors.surface,
              ),
              child: const Text('Submit Review'),
            ),
          ],
        ),
      ),
    );
  }

  List<Widget> _buildTrackingNotices() {
    return [
      if (_connectionError != null)
        MaterialBanner(
          content: Text(_connectionError!),
          actions: [
            TextButton(
              onPressed: () {
                ScaffoldMessenger.of(context).hideCurrentMaterialBanner();
                setState(() => _connectionError = null);
              },
              child: const Text('Dismiss'),
            ),
          ],
        ),
      if (_trackingStopped)
        const ListTile(
          leading: Icon(Icons.info_outline),
          title: Text('Agent tracking has stopped.'),
        ),
    ];
  }

  @override
  Widget build(BuildContext context) {
    final destination = LatLng(
      widget.destinationLatitude,
      widget.destinationLongitude,
    );

    final body = _currentStatus == 'Completed'
        ? SingleChildScrollView(
            child: Column(
              children: [
                _buildProviderCard(),
                _buildLifecycleBar(),
                _buildCompletionSummary(),
                _buildFeedbackSection(),
                _buildServiceProofSection(),
                ..._buildTrackingNotices(),
              ],
            ),
          )
        : Column(
            children: [
              _buildProviderCard(),
              Expanded(
                child: Container(
                  margin: const EdgeInsets.symmetric(horizontal: 16),
                  clipBehavior: Clip.antiAlias,
                  decoration: BoxDecoration(
                    color: AppColors.surface,
                    borderRadius: BorderRadius.circular(16),
                  ),
                  child: LiveTrackingMap(
                    jobId: widget.jobId,
                    status: _currentStatus,
                    destination: destination,
                    onTrackingStopped: () {
                      if (mounted) {
                        setState(() {
                          _trackingStopped = true;
                          _currentStatus = 'Arrived';
                        });
                      }
                    },
                    onConnectionError: (error) {
                      if (mounted) setState(() => _connectionError = error);
                    },
                  ),
                ),
              ),
              _buildLifecycleBar(),
              ..._buildTrackingNotices(),
            ],
          );

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: Text('Job #${widget.jobId} Tracking'),
        backgroundColor: AppColors.primary,
        foregroundColor: AppColors.surface,
        elevation: 0,
      ),
      body: body,
    );
  }
}
