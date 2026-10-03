import 'dart:async';

import 'package:flutter/material.dart';
import 'package:latlong2/latlong.dart';

import '../../core/api/api_client.dart';
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

class _CustomerJobTrackingScreenState
    extends State<CustomerJobTrackingScreen> {
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
        matchingRequest['status']?.toString() ?? _currentStatus,
      );
      final completion = matchingRequest['completionRecord'];
      final completionData = completion is Map
          ? Map<String, dynamic>.from(completion)
          : null;
      final polledImageUrl = completionData?['proofOfWorkImageUrl']?.toString();
      final polledSummary =
          (completionData?['summaryNotes'] ?? completionData?['workSummary'])
              ?.toString();
      final polledAdditionalCost =
          (completionData?['additionalCost'] as num?)?.toDouble();
      final polledFeedbackComment = matchingRequest['feedbackComment']?.toString();
      final polledFeedbackRating =
          (matchingRequest['feedbackRating'] as num?)?.toInt();
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
      case 'workcompleted':
        return 'Completed';
      case 'assigned':
      default:
        return 'Assigned';
    }
  }

  int get _currentStageIndex {
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
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
  }

  Widget _buildLifecycleBar() {
    return Card(
      margin: const EdgeInsets.fromLTRB(16, 16, 16, 8),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 16),
        child: Row(
          children: List.generate(_lifecycleStages.length, (index) {
            final isComplete = index <= _currentStageIndex;
            final label = _lifecycleStages[index]
                .replaceAll('OnTheWay', 'On The Way')
                .replaceAll('InProgress', 'In Progress');
            return Expanded(
              child: Column(
                children: [
                  CircleAvatar(
                    radius: 14,
                    backgroundColor:
                        isComplete ? Colors.green : Colors.grey.shade300,
                    child: Text(
                      '${index + 1}',
                      style: TextStyle(
                        color: isComplete ? Colors.white : Colors.grey.shade700,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ),
                  const SizedBox(height: 6),
                  Text(
                    label,
                    textAlign: TextAlign.center,
                    style: const TextStyle(fontSize: 10),
                  ),
                  if (index < _lifecycleStages.length - 1)
                    Divider(
                      color: index < _currentStageIndex
                          ? Colors.green
                          : Colors.grey.shade300,
                      thickness: 2,
                    ),
                ],
              ),
            );
          }),
        ),
      ),
    );
  }

  Widget _buildProviderCard() {
    final name = widget.providerName?.trim().isNotEmpty == true
        ? widget.providerName!
        : 'Assigned Provider';
    final category = widget.providerCategory?.trim().isNotEmpty == true
        ? widget.providerCategory!
        : 'Service Provider';
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
                  : const Icon(Icons.person),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(name, style: const TextStyle(fontWeight: FontWeight.bold)),
                  Text(category),
                  Text('$rating ${rating == 'Not rated' ? '' : '★'}'),
                  if (widget.providerPhone?.isNotEmpty == true)
                    Text(widget.providerPhone!),
                ],
              ),
            ),
            if (widget.providerPhone?.isNotEmpty == true)
              IconButton(
                tooltip: 'Contact provider',
                icon: const Icon(Icons.phone),
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
      color: Colors.green.shade50,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Text(
              'Thank You! Job Completed Successfully',
              style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
            ),
            if (_completionImageUrl?.isNotEmpty == true) ...[
              const SizedBox(height: 12),
              Image.network(
                _completionImageUrl!,
                height: 180,
                fit: BoxFit.cover,
                errorBuilder: (_, error, stackTrace) =>
                  const Icon(Icons.broken_image),
              ),
            ],
            if (_completionSummary?.isNotEmpty == true) ...[
              const SizedBox(height: 12),
              Text('Summary: $_completionSummary'),
            ],
            if (_additionalCost != null) ...[
              const SizedBox(height: 8),
              Text('Additional cost: $_additionalCost'),
            ],
          ],
        ),
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
            const Text('Rate your service', style: TextStyle(fontWeight: FontWeight.bold)),
            Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: List.generate(5, (index) {
                final rating = index + 1;
                return IconButton(
                  icon: Icon(
                    rating <= _selectedRating ? Icons.star : Icons.star_border,
                    color: Colors.amber,
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
              ),
            ),
            const SizedBox(height: 12),
            ElevatedButton(
              onPressed: _isSubmittingFeedback ? null : _submitFeedback,
              child: const Text('Submit Feedback'),
            ),
          ],
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final destination = LatLng(
      widget.destinationLatitude,
      widget.destinationLongitude,
    );

    return Scaffold(
      appBar: AppBar(title: Text('Job #${widget.jobId} Tracking')),
      body: Column(
        children: [
          _buildLifecycleBar(),
          _buildProviderCard(),
          if (_currentStatus == 'Completed') ...[
            _buildCompletionSummary(),
            _buildFeedbackSection(),
          ],
          if (_connectionError != null)
            MaterialBanner(
              content: Text(_connectionError!),
              actions: const [],
            ),
          if (_trackingStopped)
            const ListTile(
              leading: Icon(Icons.info_outline),
              title: Text('Provider tracking has stopped.'),
            ),
          Expanded(
            child: _currentStatus == 'Completed'
                ? const SizedBox.shrink()
                : LiveTrackingMap(
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
        ],
      ),
    );
  }
}