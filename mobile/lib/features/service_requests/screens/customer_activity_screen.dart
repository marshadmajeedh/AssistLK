import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../../shared/theme/app_assets.dart';
import '../../../../shared/theme/app_spacing.dart';
import '../../../../shared/widgets/app_button.dart';
import '../../../../shared/widgets/app_card.dart';
import '../../../../shared/widgets/empty_state_card.dart';
import '../../../../shared/widgets/section_header.dart';
import '../providers/service_request_provider.dart';
import '../widgets/service_request_card.dart';
import 'create_service_request_screen.dart';
import 'service_request_detail_screen.dart';

class CustomerActivityScreen extends StatelessWidget {
  const CustomerActivityScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<ServiceRequestProvider>();
    void create() => Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => const CreateServiceRequestScreen(),
      ),
    );
    Future<void> refresh() async {
      if (!provider.isLoading) await provider.loadMyRequests();
    }

    return RefreshIndicator(
      onRefresh: refresh,
      child: CustomScrollView(
        key: const PageStorageKey('customer-activity'),
        primary: false,
        physics: const AlwaysScrollableScrollPhysics(),
        slivers: [
          SliverPadding(
            padding: const EdgeInsets.all(AppSpacing.lg),
            sliver: SliverList.list(
              children: [
                LayoutBuilder(
                  builder: (context, constraints) {
                    final action = ElevatedButton.icon(
                      onPressed: create,
                      icon: const Icon(Icons.add, size: 18),
                      label: const Text('Create Request'),
                    );
                    if (constraints.maxWidth <
                        320 * MediaQuery.textScalerOf(context).scale(1)) {
                      return Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          const SectionHeader(title: 'My Requests'),
                          const SizedBox(height: AppSpacing.sm),
                          action,
                        ],
                      );
                    }
                    return SectionHeader(title: 'My Requests', action: action);
                  },
                ),
                const SizedBox(height: AppSpacing.md),
                if (provider.isLoading)
                  const Padding(
                    padding: EdgeInsets.all(AppSpacing.md),
                    child: Center(child: CircularProgressIndicator()),
                  ),
                if (provider.error != null) ...[
                  AppCard(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        Text(
                          provider.error!,
                          semanticsLabel:
                              'Request loading error: ${provider.error}',
                        ),
                        const SizedBox(height: AppSpacing.sm),
                        AppButton(
                          text: 'Retry',
                          onPressed: provider.isLoading ? null : refresh,
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: AppSpacing.md),
                ],
                if (!provider.isLoading &&
                    provider.error == null &&
                    provider.requests.isEmpty)
                  EmptyStateCard(
                    title: 'No Service Requests Yet',
                    description: "You haven't created any service requests. Describe your problem to get started with AssistLK AI.",
                    imageAsset: AppAssets.emptyRequests,
                    actionText: 'Create Request',
                    onAction: create,
                  ),
              ],
            ),
          ),
          SliverPadding(
            padding: const EdgeInsets.fromLTRB(
              AppSpacing.lg,
              0,
              AppSpacing.lg,
              AppSpacing.lg,
            ),
            sliver: SliverList.builder(
              itemCount: provider.requests.length,
              itemBuilder: (context, index) {
                final request = provider.requests[index];
                return Padding(
                  padding: const EdgeInsets.only(bottom: AppSpacing.sm),
                  child: ServiceRequestCard(
                    request: request,
                    onTap: () => Navigator.of(context).push(
                      MaterialPageRoute<void>(
                        builder: (_) => ServiceRequestDetailScreen(
                          requestId: request.serviceRequestId,
                        ),
                      ),
                    ),
                  ),
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}
