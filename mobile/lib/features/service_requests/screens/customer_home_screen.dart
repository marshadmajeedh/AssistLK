import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../../shared/theme/app_assets.dart';
import '../../../../shared/theme/app_colors.dart';
import '../../../../shared/theme/app_spacing.dart';
import '../../../../shared/theme/app_text_styles.dart';
import '../../../../shared/widgets/app_button.dart';
import '../../../../shared/widgets/app_card.dart';
import '../../../../shared/widgets/app_image_asset.dart';
import '../../../../shared/widgets/section_header.dart';
import '../../auth/providers/auth_provider.dart';
import '../../customer/widgets/home_location_banner.dart';
import '../navigation/open_create_service_request.dart';
import '../providers/service_request_provider.dart';
import '../widgets/service_category_shortcuts.dart';
import '../widgets/service_request_card.dart';
import 'service_request_detail_screen.dart';

class CustomerHomeScreen extends StatelessWidget {
  final VoidCallback? onViewAll;
  const CustomerHomeScreen({super.key, this.onViewAll});

  @override
  Widget build(BuildContext context) {
    final user = context.watch<AuthProvider>().user;
    final requests = context.watch<ServiceRequestProvider>();
    final sorted = requests.requests.toList()
      ..sort((a, b) {
        final byDate = b.createdAt.compareTo(a.createdAt);
        return byDate != 0
            ? byDate
            : a.serviceRequestId.compareTo(b.serviceRequestId);
      });
    final recent = sorted.take(3);
    return RefreshIndicator(
      onRefresh: () async {
        if (!requests.isLoading) await requests.loadMyRequests();
      },
      child: SingleChildScrollView(
        key: const PageStorageKey('customer-home'),
        primary: false,
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(AppSpacing.lg),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const HomeLocationBanner(),
            const SizedBox(height: AppSpacing.lg),
            Text(
              'Welcome, ${user?.fullName ?? 'Customer'}',
              style: AppTextStyles.pageTitle,
            ),
            const SizedBox(height: AppSpacing.sm),
            const Text(
              'What can we help you with today?',
              style: AppTextStyles.body,
            ),
            const SizedBox(height: AppSpacing.md),
            AppButton(
              text: 'Create Service Request',
              onPressed: () => openCreateServiceRequest(context),
            ),
            const SizedBox(height: AppSpacing.lg),
            const SectionHeader(title: 'Explore services'),
            const SizedBox(height: AppSpacing.md),
            ServiceCategoryShortcuts(
              onSelected: (category) => openCreateServiceRequest(
                context,
                categoryHint: category.canonicalName,
              ),
            ),
            const SizedBox(height: AppSpacing.lg),
            AppCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const Align(
                    alignment: Alignment.centerLeft,
                    child: AppImageAsset(
                      assetPath: AppAssets.aiDiagnosisSpark,
                      width: 48,
                      height: 48,
                      fallbackIcon: Icons.auto_awesome_rounded,
                      semanticLabel: 'AssistLK AI',
                    ),
                  ),
                  const SizedBox(height: AppSpacing.sm),
                  const Text(
                    'Not sure what service you need?',
                    style: AppTextStyles.cardHeading,
                  ),
                  const SizedBox(height: AppSpacing.sm),
                  const Text(
                    'Describe the problem and AssistLK AI will help identify the appropriate service category.',
                    style: AppTextStyles.body,
                  ),
                  const SizedBox(height: AppSpacing.md),
                  OutlinedButton(
                    onPressed: () => openCreateServiceRequest(context),
                    child: const Text('Describe Problem'),
                  ),
                ],
              ),
            ),
            const SizedBox(height: AppSpacing.lg),
            const SectionHeader(title: 'Recent Activity'),
            Align(
              alignment: Alignment.centerLeft,
              child: TextButton(
                onPressed: onViewAll,
                child: const Text('View All'),
              ),
            ),
            if (requests.isLoading && recent.isEmpty)
              const LinearProgressIndicator()
            else if (requests.error != null)
              Text(
                'Unable to refresh activity. Pull down to try again.',
                style: AppTextStyles.small.copyWith(color: AppColors.error),
              )
            else if (recent.isEmpty)
              const Text('No recent requests yet.', style: AppTextStyles.body),
            for (final request in recent)
              Padding(
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
              ),
          ],
        ),
      ),
    );
  }
}
