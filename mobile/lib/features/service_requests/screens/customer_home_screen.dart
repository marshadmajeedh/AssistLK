import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../../shared/theme/app_colors.dart';
import '../../../../shared/theme/app_radius.dart';
import '../../../../shared/theme/app_spacing.dart';
import '../../../../shared/theme/app_text_styles.dart';
import '../../../../shared/widgets/app_button.dart';
import '../../auth/providers/auth_provider.dart';
import '../providers/service_request_provider.dart';
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

  void _navigateToCreate() {
    Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => const CreateServiceRequestScreen(),
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
              // Welcome Section
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

              // Action Row: "My Service Requests" Title and "Create Request" Button
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  const Text(
                    'My Service Requests',
                    style: AppTextStyles.sectionHeading,
                  ),
                  ElevatedButton.icon(
                    onPressed: _navigateToCreate,
                    icon: const Icon(Icons.add, size: 18),
                    label: const Text('Create Request'),
                    style: ElevatedButton.styleFrom(
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

              // Request list or Loading or Empty State
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
              onPressed: _navigateToCreate,
            ),
          ),
        ],
      ),
    );
  }
}
