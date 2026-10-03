import 'package:flutter/material.dart';

import '../../core/api/api_client.dart';
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
    return Scaffold(
      appBar: AppBar(title: Text("Job #${widget.jobId} Status")),
      body: Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Text("Current Status: $currentStatus", style: const TextStyle(fontSize: 20, fontWeight: FontWeight.bold)),
            const SizedBox(height: 30),
            if (currentStatus == "Assigned")
              ElevatedButton(
                onPressed: _isUpdatingStatus ? null : () => updateStatus("OnTheWay"),
                child: const Text("Mark as On The Way"),
              ),
            if (currentStatus == "OnTheWay")
              ElevatedButton(
                onPressed: _isUpdatingStatus ? null : () => updateStatus("Arrived"),
                child: const Text("Mark as Arrived"),
              ),
            if (currentStatus == "Arrived")
              ElevatedButton(onPressed: () => updateStatus("InProgress"), child: const Text("Start Work (In Progress)")),
            if (currentStatus == "InProgress")
              ElevatedButton(onPressed: () => updateStatus("Completed"), child: const Text("Mark as Completed")),
          ],
        ),
      ),
    );
  }
}