import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../../shared/theme/app_assets.dart';
import '../../../../shared/theme/app_colors.dart';
import '../../../../shared/theme/app_radius.dart';
import '../../../../shared/theme/app_spacing.dart';
import '../../../../shared/theme/app_text_styles.dart';
import '../../../../shared/widgets/app_button.dart';
import '../../../../shared/widgets/app_card.dart';
import '../../../../shared/widgets/app_image_asset.dart';
import '../../../../shared/widgets/empty_state_card.dart';
import '../../../../shared/widgets/section_header.dart';
import '../../auth/providers/auth_provider.dart';
import '../../customer/widgets/home_location_banner.dart';
import '../../tracking/customer_job_tracking_screen.dart';
import '../models/service_request_status.dart';
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
            Container(
              width: double.infinity,
              constraints: const BoxConstraints(minHeight: 135),
              decoration: BoxDecoration(
                borderRadius: BorderRadius.circular(AppRadius.large),
                border: Border.all(color: AppColors.border),
                color: AppColors.surface,
              ),
              clipBehavior: Clip.antiAlias,
              child: Stack(
                alignment: Alignment.centerLeft,
                children: [
                  const Positioned.fill(
                    child: AppImageAsset(
                      assetPath: AppAssets.homeServiceHero,
                      fit: BoxFit.cover,
                      fallbackIcon: Icons.home_repair_service_rounded,
                      semanticLabel: 'Home Service Assistance',
                    ),
                  ),
                  Positioned.fill(
                    child: Container(
                      decoration: BoxDecoration(
                        gradient: LinearGradient(
                          begin: Alignment.centerLeft,
                          end: Alignment.centerRight,
                          colors: [
                            AppColors.primaryDark.withValues(alpha: 0.92),
                            AppColors.primaryDark.withValues(alpha: 0.70),
                            Colors.transparent,
                          ],
                        ),
                      ),
                    ),
                  ),
                  Padding(
                    padding: const EdgeInsets.symmetric(
                      horizontal: AppSpacing.md,
                      vertical: AppSpacing.sm,
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisAlignment: MainAxisAlignment.center,
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(
                          'Welcome, ${user?.fullName ?? 'Customer'}',
                          style: AppTextStyles.sectionHeading.copyWith(
                            color: Colors.white,
                            fontWeight: FontWeight.w700,
                          ),
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                        ),
                        const SizedBox(height: AppSpacing.xs),
                        Text(
                          'What can we help you with today?',
                          style: AppTextStyles.body.copyWith(
                            color: Colors.white.withValues(alpha: 0.90),
                          ),
                          maxLines: 2,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: AppSpacing.lg),
            AppButton(
              text: 'Create Service Request',
              onPressed: () => openCreateServiceRequest(context),
            ),
            // TODO: Remove after Component 4 navigation is finalized
            OutlinedButton.icon(
              icon: const Icon(Icons.location_on),
              label: const Text('Test C4 (Tracking)'),
              onPressed: () {
                final activeRequest = requests.requests
                    .where(
                      (request) =>
                          request.serviceJobId != null &&
                          request.latitude != null &&
                          request.longitude != null &&
                          request.status != ServiceRequestStatus.completed &&
                          request.status != ServiceRequestStatus.cancelled,
                    )
                    .toList()
                  ..sort((a, b) => b.updatedAt.compareTo(a.updatedAt));

                if (activeRequest.isEmpty) {
                  ScaffoldMessenger.of(context).showSnackBar(
                    const SnackBar(
                      content: Text('No active service jobs found to track'),
                    ),
                  );
                  return;
                }

                final request = activeRequest.first;
                Navigator.of(context).push(
                  MaterialPageRoute<void>(
                    builder: (_) => CustomerJobTrackingScreen(
                      jobId: request.serviceJobId!,
                      status: request.jobStatus ?? request.status.toJson(),
                      destinationLatitude: request.latitude!,
                      destinationLongitude: request.longitude!,
                      completionImageUrl:
                          request.completionRecord?.proofOfWorkImageUrl,
                      completionSummary: request.completionRecord?.summaryNotes,
                      hasFeedback: request.hasFeedback,
                      feedbackRating: request.feedbackRating,
                      feedbackComment: request.feedbackComment,
                    ),
                  ),
                );
              },
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
            if (recent.isNotEmpty)
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
              EmptyStateCard(
                title: 'No service requests yet',
                description:
                    'Create your first request and track its progress here.',
                imageAsset: AppAssets.emptyRecentActivity,
                imageSemanticLabel: 'No recent activity illustration',
                padding: const EdgeInsets.symmetric(
                  horizontal: AppSpacing.lg,
                  vertical: AppSpacing.xl,
                ),
                imageWidth: 96,
                imageHeight: 96,
                actionText: 'Create Service Request',
                actionWidth: 220,
                onAction: () => openCreateServiceRequest(context),
              )
            else
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
