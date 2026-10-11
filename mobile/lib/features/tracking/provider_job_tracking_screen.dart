import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/api/api_client.dart';
import '../../shared/theme/app_colors.dart';
import '../providers/providers/provider_dashboard_provider.dart';
import '../providers/services/provider_service.dart';
import 'services/provider_location_stream_service.dart';
import 'widgets/proof_of_work_upload_widget.dart';

class ProviderJobTrackingScreen extends StatefulWidget {
  final String jobId;
  final String initialStatus;
  final double? customerLatitude;
  final double? customerLongitude;
  final String? customerAddress;
  final String? serviceDescription;

  const ProviderJobTrackingScreen({
    super.key,
    required this.jobId,
    this.initialStatus = 'Assigned',
    this.customerLatitude,
    this.customerLongitude,
    this.customerAddress,
    this.serviceDescription,
  });

  @override
  State<ProviderJobTrackingScreen> createState() => _ProviderJobTrackingScreenState();
}

class _ProviderJobTrackingScreenState extends State<ProviderJobTrackingScreen> {
  late String currentStatus;
  late final ProviderLocationStreamService _locationStreamService;
  bool _isUpdatingStatus = false;
  DateTime? _jobStartedAt;

  @override
  void initState() {
    super.initState();
    currentStatus = _normalizeStatus(widget.initialStatus);
    _locationStreamService = ProviderLocationStreamService();
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

  Future<void> updateStatus(String nextStatus) async {
    if (nextStatus == "Completed") {
      await _showCompletionModal();
      return;
    }

    if (_isUpdatingStatus) return;
    setState(() => _isUpdatingStatus = true);
    try {
      await ProviderService(apiClient: ApiClient()).updateJobStatus(
        widget.jobId,
        nextStatus,
      );

      if (mounted) setState(() => currentStatus = nextStatus);

      if (nextStatus == 'OnTheWay') {
        try {
          await _locationStreamService.start(
            jobId: widget.jobId,
            isOnTheWay: true,
            onStopped: () {
              if (mounted) setState(() => currentStatus = 'Arrived');
            },
          );
        } catch (error) {
          if (mounted) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(
                content: Text(
                  'Status updated to On The Way, but live location could not start: $error',
                ),
              ),
            );
          }
        }
      } else if (nextStatus == 'Arrived') {
        try {
          await _locationStreamService.stop();
        } catch (error) {
          if (mounted) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(content: Text('Live location stop failed: $error')),
            );
          }
        }
      } else if (nextStatus == 'InProgress') {
        _jobStartedAt = DateTime.now();
      }
    } catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Status update failed: $error')),
        );
      }
    } finally {
      if (mounted) setState(() => _isUpdatingStatus = false);
    }
  }

  Future<void> _showCompletionModal() async {
    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      builder: (modalContext) => ProofOfWorkUploadWidget(
        jobId: widget.jobId,
        timeElapsedMinutes: _jobStartedAt == null
            ? 0
            : DateTime.now().difference(_jobStartedAt!).inSeconds / 60,
        onCompleted: (response) async {
          if (!modalContext.mounted) return;
          Navigator.pop(modalContext);
          final dashboard = context.read<ProviderDashboardProvider>();
          dashboard.markJobCompletedLocally(widget.jobId);
          await dashboard.completeJob();
          await _locationStreamService.stop();
          if (mounted) setState(() => currentStatus = 'Completed');
        },
      ),
    );
  }

  @override
  void dispose() {
    _locationStreamService.stop();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final customerAddress = widget.customerAddress?.trim();
    final serviceDescription = widget.serviceDescription?.trim();
    final hasCoordinates =
        widget.customerLatitude != null && widget.customerLongitude != null;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: Text('Job #${widget.jobId} Status'),
        backgroundColor: AppColors.surface,
        foregroundColor: AppColors.textPrimary,
        elevation: 0,
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(20, 24, 20, 32),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
              'Service Progress',
              style: TextStyle(
                color: AppColors.textPrimary,
                fontSize: 22,
                fontWeight: FontWeight.w700,
              ),
            ),
            const SizedBox(height: 18),
            _buildProgressTimeline(),
            if ((serviceDescription?.isNotEmpty ?? false) ||
                (customerAddress?.isNotEmpty ?? false) ||
                hasCoordinates) ...[
              const SizedBox(height: 20),
              const Text(
                'Service Details',
                style: TextStyle(
                  color: AppColors.textPrimary,
                  fontSize: 16,
                  fontWeight: FontWeight.w700,
                ),
              ),
              const SizedBox(height: 12),
              if (serviceDescription?.isNotEmpty ?? false)
                _buildDetailRow(
                  icon: Icons.handyman_outlined,
                  label: 'Service',
                  value: serviceDescription!,
                ),
              if (customerAddress?.isNotEmpty ?? false)
                _buildDetailRow(
                  icon: Icons.location_on_outlined,
                  label: 'Customer location',
                  value: customerAddress!,
                )
              else if (hasCoordinates)
                _buildDetailRow(
                  icon: Icons.location_on_outlined,
                  label: 'Customer location',
                  value:
                      '${widget.customerLatitude!.toStringAsFixed(5)}, '
                      '${widget.customerLongitude!.toStringAsFixed(5)}',
                ),
            ],
            const SizedBox(height: 24),
            if (currentStatus == 'Assigned')
              SizedBox(
                width: double.infinity,
                child: ElevatedButton.icon(
                  onPressed: _isUpdatingStatus
                      ? null
                      : () => updateStatus('OnTheWay'),
                  icon: const Icon(Icons.directions_car_outlined),
                  label: const Text('Mark as On The Way'),
                ),
              ),
            if (currentStatus == 'OnTheWay')
              SizedBox(
                width: double.infinity,
                child: ElevatedButton.icon(
                  onPressed: _isUpdatingStatus
                      ? null
                      : () => updateStatus('Arrived'),
                  icon: const Icon(Icons.location_on_outlined),
                  label: const Text('Mark as Arrived'),
                ),
              ),
            if (currentStatus == 'Arrived')
              SizedBox(
                width: double.infinity,
                child: ElevatedButton.icon(
                  onPressed: () => updateStatus('InProgress'),
                  icon: const Icon(Icons.handyman_outlined),
                  label: const Text('Start Work (In Progress)'),
                ),
              ),
            if (currentStatus == 'InProgress')
              SizedBox(
                width: double.infinity,
                child: ElevatedButton.icon(
                  onPressed: () => updateStatus('Completed'),
                  icon: const Icon(Icons.check_circle_outline),
                  label: const Text('Mark as Completed'),
                ),
              ),
          ],
        ),
      ),
    );
  }

  Widget _buildProgressTimeline() {
    const stages = [
      'Assigned',
      'On The Way',
      'Arrived',
      'In Progress',
      'Completed',
    ];
    const statuses = [
      'Assigned',
      'OnTheWay',
      'Arrived',
      'InProgress',
      'Completed',
    ];
    const icons = [
      Icons.assignment_outlined,
      Icons.directions_car_outlined,
      Icons.location_on_outlined,
      Icons.handyman_outlined,
      Icons.check_circle_outline,
    ];
    final currentIndex = statuses.indexOf(currentStatus);

    return Column(
      children: List.generate(stages.length, (index) {
        final isComplete =
            currentStatus == 'Completed' || index < currentIndex;
        final isCurrent = currentStatus != 'Completed' &&
            index == currentIndex;
        final isLast = index == stages.length - 1;
        final connectorColor = isComplete
            ? AppColors.success
            : AppColors.border;

        return Padding(
          padding: EdgeInsets.only(bottom: isLast ? 0 : 4),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              SizedBox(
                width: 44,
                child: Column(
                  children: [
                    Container(
                      key: ValueKey('tracking_step_${statuses[index]}_indicator'),
                      width: 32,
                      height: 32,
                      decoration: BoxDecoration(
                        color: isComplete
                            ? AppColors.success
                            : isCurrent
                                ? AppColors.primary
                                : AppColors.background,
                        shape: BoxShape.circle,
                        border: isComplete || isCurrent
                            ? null
                            : Border.all(color: AppColors.disabled, width: 2),
                      ),
                      child: isComplete
                          ? const Icon(Icons.check, color: Colors.white, size: 19)
                          : isCurrent
                              ? Icon(icons[index], color: Colors.white, size: 17)
                              : null,
                    ),
                    if (!isLast)
                      Container(
                        width: 2,
                        height: 38,
                        color: connectorColor,
                      ),
                  ],
                ),
              ),
              Expanded(
                child: AnimatedContainer(
                  key: ValueKey('tracking_step_${statuses[index]}_content'),
                  duration: const Duration(milliseconds: 180),
                  margin: const EdgeInsets.only(bottom: 8),
                  padding: const EdgeInsets.symmetric(
                    horizontal: 14,
                    vertical: 10,
                  ),
                  decoration: BoxDecoration(
                    color: isCurrent ? AppColors.primarySurface : null,
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        stages[index],
                        style: TextStyle(
                          color: isComplete
                              ? AppColors.success
                              : isCurrent
                                  ? AppColors.primary
                                  : AppColors.textSecondary,
                          fontSize: 15,
                          fontWeight: isComplete || isCurrent
                              ? FontWeight.w700
                              : FontWeight.w500,
                        ),
                      ),
                      if (isCurrent) ...[
                        const SizedBox(height: 3),
                        const Text(
                          'You are currently working on this',
                          style: TextStyle(
                            color: AppColors.primary,
                            fontSize: 12,
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
              ),
            ],
          ),
        );
      }),
    );
  }

  Widget _buildDetailRow({
    required IconData icon,
    required String label,
    required String value,
  }) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, color: AppColors.primary, size: 20),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  label,
                  style: const TextStyle(
                    color: AppColors.textSecondary,
                    fontSize: 12,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  value,
                  style: const TextStyle(
                    color: AppColors.textPrimary,
                    fontSize: 14,
                    fontWeight: FontWeight.w500,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}