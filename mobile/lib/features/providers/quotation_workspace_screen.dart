import 'package:flutter/material.dart';

import '../tracking/provider_job_tracking_screen.dart';

class QuotationWorkspaceScreen extends StatelessWidget {
  final String serviceJobId;
  final String status;

  const QuotationWorkspaceScreen({
    super.key,
    required this.serviceJobId,
    required this.status,
  });

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Quotation Workspace')),
      body: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Component 3: Quotation & Booking',
              style: Theme.of(context).textTheme.headlineSmall,
            ),
            const SizedBox(height: 16),
            Text('Service job: $serviceJobId'),
            const SizedBox(height: 8),
            Text('Current status: $status'),
            const SizedBox(height: 24),
            const Text(
              'The approved match is ready for quotation and booking coordination.',
            ),
            const SizedBox(height: 24),
            // TODO: Remove after Component 3 is done
            SizedBox(
              width: double.infinity,
              child: ElevatedButton.icon(
                icon: const Icon(Icons.skip_next),
                label: const Text('Skip to Component 4 (Test Tracking)'),
                onPressed: () {
                  Navigator.of(context).push(
                    MaterialPageRoute<void>(
                      builder: (_) => ProviderJobTrackingScreen(
                        jobId: serviceJobId,
                      ),
                    ),
                  );
                },
              ),
            ),
          ],
        ),
      ),
    );
  }
}
