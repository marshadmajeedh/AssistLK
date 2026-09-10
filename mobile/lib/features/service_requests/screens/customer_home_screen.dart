import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../../shared/theme/app_colors.dart';
import '../../../../shared/theme/app_radius.dart';
import '../../../../shared/theme/app_spacing.dart';
import '../../../../shared/theme/app_text_styles.dart';
import '../../../../shared/widgets/app_button.dart';
import '../../auth/providers/auth_provider.dart';
import '../models/canonical_service_category.dart';
import '../providers/service_request_provider.dart';
import '../widgets/service_category_card.dart';
import '../widgets/service_request_card.dart';
import 'create_service_request_screen.dart';
import 'service_request_detail_screen.dart';

class CustomerHomeScreen extends StatefulWidget {
  const CustomerHomeScreen({super.key});

  @override
  State<CustomerHomeScreen> createState() => _CustomerHomeScreenState();
}

class _CustomerHomeScreenState extends State<CustomerHomeScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<ServiceRequestProvider>().loadMyRequests();
    });
  }

  void _navigateToCreate({String? categoryPreference}) {
    Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => CreateServiceRequestScreen(
          initialCategoryPreference: categoryPreference,
        ),
      ),
    );
  }

  void _navigateToDetail(String requestId) {
    Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => ServiceRequestDetailScreen(requestId: requestId),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();
    final requestProvider = context.watch<ServiceRequestProvider>();
    final requests = requestProvider.requests;

    return Scaffold(
      appBar: AppBar(
        title: const Text('AssistLK Customer'),
        actions: [
          IconButton(
            icon: const Icon(Icons.logout_rounded),
            tooltip: 'Logout',
            onPressed: () async {
              await context.read<AuthProvider>().logout();
            },
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () => requestProvider.loadMyRequests(),
        child: SingleChildScrollView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.all(AppSpacing.lg),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // 1. Welcome Section
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(AppSpacing.lg),
                decoration: BoxDecoration(
                  gradient: const LinearGradient(
                    colors: [AppColors.primary, AppColors.primaryDark],
                    begin: Alignment.topLeft,
                    end: Alignment.bottomRight,
                  ),
                  borderRadius: BorderRadius.circular(AppRadius.large),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Welcome, ${auth.user?.fullName ?? 'Customer'}',
                      style: AppTextStyles.sectionHeading.copyWith(
                        color: Colors.white,
                      ),
                    ),
                    const SizedBox(height: AppSpacing.xs),
                    Text(
                      auth.user?.email ?? '',
                      style: AppTextStyles.small.copyWith(
                        color: Colors.white.withValues(alpha: 0.8),
                      ),
                    ),
                    const SizedBox(height: AppSpacing.md),
                    const Text(
                      'Need help with plumbing, electrical, or home repairs? Create a request and let Gemini AI understand your problem.',
                      style: TextStyle(
                        fontSize: 13,
                        color: Colors.white,
                        height: 1.4,
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: AppSpacing.lg),

              // 2. Service Shortcuts Section
              const Text(
                'What do you need help with?',
                style: AppTextStyles.sectionHeading,
              ),
              const SizedBox(height: AppSpacing.xs),
              Text(
                'Choose a service category to get started quickly',
                style: AppTextStyles.small.copyWith(
                  color: AppColors.textSecondary,
                ),
              ),
              const SizedBox(height: AppSpacing.md),

              // 2x2 Grid of Canonical Service Categories
              Row(
                children: [
                  Expanded(
                    child: ServiceCategoryCard(
                      category: CanonicalServiceCategory.plumbing,
                      onTap: () => _navigateToCreate(
                        categoryPreference:
                            CanonicalServiceCategory.plumbing.canonicalName,
                      ),
                    ),
                  ),
                  const SizedBox(width: AppSpacing.sm),
                  Expanded(
                    child: ServiceCategoryCard(
                      category: CanonicalServiceCategory.electrical,
                      onTap: () => _navigateToCreate(
                        categoryPreference:
                            CanonicalServiceCategory.electrical.canonicalName,
                      ),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: AppSpacing.sm),
              Row(
                children: [
                  Expanded(
                    child: ServiceCategoryCard(
                      category: CanonicalServiceCategory.vehicleRepair,
                      onTap: () => _navigateToCreate(
                        categoryPreference:
                            CanonicalServiceCategory.vehicleRepair.canonicalName,
                      ),
                    ),
                  ),
                  const SizedBox(width: AppSpacing.sm),
                  Expanded(
                    child: ServiceCategoryCard(
                      category: CanonicalServiceCategory.applianceRepair,
                      onTap: () => _navigateToCreate(
                        categoryPreference:
                            CanonicalServiceCategory.applianceRepair.canonicalName,
                      ),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: AppSpacing.lg),

              // 3. AI Assistance Option Card
              Material(
                color: Colors.transparent,
                child: InkWell(
                  onTap: () => _navigateToCreate(categoryPreference: null),
                  borderRadius: BorderRadius.circular(AppRadius.large),
                  child: Container(
                    width: double.infinity,
                    padding: const EdgeInsets.all(AppSpacing.md),
                    decoration: BoxDecoration(
                      color: AppColors.primary.withValues(alpha: 0.04),
                      borderRadius: BorderRadius.circular(AppRadius.large),
                      border: Border.all(
                        color: AppColors.primary.withValues(alpha: 0.25),
                      ),
                    ),
                    child: Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Container(
                          padding: const EdgeInsets.all(AppSpacing.sm),
                          decoration: BoxDecoration(
                            color: AppColors.primary.withValues(alpha: 0.1),
                            borderRadius: BorderRadius.circular(AppRadius.medium),
                          ),
                          child: const Icon(
                            Icons.auto_awesome_rounded,
                            color: AppColors.primary,
                            size: 24,
                          ),
                        ),
                        const SizedBox(width: AppSpacing.md),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              const Text(
                                'Not sure what service you need?',
                                style: AppTextStyles.cardHeading,
                              ),
                              const SizedBox(height: AppSpacing.xs),
                              Text(
                                'Describe your problem in plain language and let AssistLK AI identify the right service and urgency.',
                                style: AppTextStyles.body.copyWith(
                                  color: AppColors.textSecondary,
                                ),
                              ),
                              const SizedBox(height: AppSpacing.sm),
                              Row(
                                mainAxisSize: MainAxisSize.min,
                                children: [
                                  Flexible(
                                    child: Text(
                                      'Let AI understand your problem',
                                      style: AppTextStyles.small.copyWith(
                                        color: AppColors.primary,
                                        fontWeight: FontWeight.w600,
                                      ),
                                      overflow: TextOverflow.ellipsis,
                                    ),
                                  ),
                                  const SizedBox(width: 4),
                                  const Icon(
                                    Icons.arrow_forward_rounded,
                                    size: 16,
                                    color: AppColors.primary,
                                  ),
                                ],
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
              const SizedBox(height: AppSpacing.lg),

              // 4. Action Row: "My Service Requests" Title and "Create Request" Button
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  const Expanded(
                    child: Text(
                      'My Service Requests',
                      style: AppTextStyles.sectionHeading,
                      overflow: TextOverflow.ellipsis,
                    ),
                  ),
                  const SizedBox(width: AppSpacing.sm),
                  ElevatedButton.icon(
                    onPressed: () => _navigateToCreate(),
                    icon: const Icon(Icons.add, size: 18),
                    label: const Text('Create Request'),
                    style: ElevatedButton.styleFrom(
                      minimumSize: const Size(0, 40),
                      padding: const EdgeInsets.symmetric(
                        horizontal: AppSpacing.md,
                        vertical: AppSpacing.sm,
                      ),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(AppRadius.medium),
                      ),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: AppSpacing.md),

              // 5. Request list or Loading or Empty State
              if (requestProvider.isLoading && requests.isEmpty) ...[
                const Padding(
                  padding: EdgeInsets.symmetric(vertical: 40),
                  child: Center(child: CircularProgressIndicator()),
                ),
              ] else if (requests.isEmpty) ...[
                _buildEmptyState(),
              ] else ...[
                ListView.separated(
                  shrinkWrap: true,
                  physics: const NeverScrollableScrollPhysics(),
                  itemCount: requests.length,
                  separatorBuilder: (_, _) =>
                      const SizedBox(height: AppSpacing.sm),
                  itemBuilder: (context, index) {
                    final request = requests[index];
                    return ServiceRequestCard(
                      request: request,
                      onTap: () => _navigateToDetail(request.serviceRequestId),
                    );
                  },
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildEmptyState() {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.lg,
        vertical: 40,
      ),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(AppRadius.large),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Container(
            padding: const EdgeInsets.all(AppSpacing.md),
            decoration: BoxDecoration(
              color: AppColors.primary.withValues(alpha: 0.08),
              shape: BoxShape.circle,
            ),
            child: const Icon(
              Icons.assignment_outlined,
              size: 40,
              color: AppColors.primary,
            ),
          ),
          const SizedBox(height: AppSpacing.md),
          const Text(
            'No Service Requests Yet',
            style: AppTextStyles.cardHeading,
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: AppSpacing.xs),
          Text(
            'You haven\'t created any service requests. Describe your problem to get started with AI diagnosis.',
            style: AppTextStyles.body.copyWith(
              color: AppColors.textSecondary,
            ),
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: AppSpacing.lg),
          SizedBox(
            width: 200,
            child: AppButton(
              text: 'Create Request',
              onPressed: () => _navigateToCreate(),
            ),
          ),
        ],
      ),
    );
  }
}
