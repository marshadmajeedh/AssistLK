import 'package:flutter/material.dart';

import '../../core/api/api_client.dart';
import 'provider_job_tracking_screen.dart';
import 'tracking_job_resolver.dart';
import 'package:provider/provider.dart';

import '../providers/providers/provider_dashboard_provider.dart';

/// Self-contained "Open Job Tracking" button for an accepted dispatch match.
class OpenJobTrackingButton extends StatelessWidget {
  final ApiClient apiClient;
  final Map<String, dynamic>? match;

  const OpenJobTrackingButton({
    super.key,
    required this.apiClient,
    required this.match,
  });

  Future<void> _open(BuildContext context) async {
    final current = match;
    final matchId = current?['jobId']?.toString();
    if (current == null || matchId == null || matchId.isEmpty) return;

    final messenger = ScaffoldMessenger.of(context);
    final navigator = Navigator.of(context);
        final dashboardProvider = context.read<ProviderDashboardProvider>();
    try {
      final serviceJobId = await resolveServiceJobId(apiClient, matchId);
      if (serviceJobId == null || serviceJobId.isEmpty) return;
            navigator.push(
        MaterialPageRoute(
          builder: (_) => ChangeNotifierProvider<ProviderDashboardProvider>.value(
            value: dashboardProvider,
            child: ProviderJobTrackingScreen(
              jobId: serviceJobId,
              initialStatus: current['jobStatus']?.toString() ?? 'Assigned',
              customerLatitude: (current['customerLatitude'] as num?)?.toDouble(),
              customerLongitude: (current['customerLongitude'] as num?)?.toDouble(),
              customerAddress: current['locationText']?.toString(),
              serviceDescription: current['description']?.toString(),
            ),
          ),
        ),
      );
    } catch (e) {
      messenger.showSnackBar(
        SnackBar(content: Text('Could not open job tracking: $e')),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: double.infinity,
      child: ElevatedButton.icon(
        icon: const Icon(Icons.route, size: 18),
        label: const Text('Open Job Tracking'),
        onPressed: () => _open(context),
      ),
    );
  }
}