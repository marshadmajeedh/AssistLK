import 'package:flutter/material.dart';

import '../../../../shared/theme/app_assets.dart';
import '../../../../shared/theme/app_colors.dart';
import '../../../../shared/theme/app_radius.dart';
import '../../../../shared/theme/app_spacing.dart';
import '../../../../shared/theme/app_text_styles.dart';
import '../../../../shared/widgets/app_image_asset.dart';
import '../../../../shared/widgets/section_header.dart';
import '../models/canonical_service_category.dart';
import '../widgets/service_category_card.dart';
import 'create_service_request_screen.dart';

class CustomerServicesScreen extends StatelessWidget {
  const CustomerServicesScreen({super.key});
  @override
  Widget build(BuildContext context) {
    void navigateToCreate({String? categoryPreference}) {
      Navigator.of(context).push(
        MaterialPageRoute<void>(
          builder: (_) => CreateServiceRequestScreen(
            initialCategoryPreference: categoryPreference,
          ),
        ),
      );
    }

    return SingleChildScrollView(
      key: const PageStorageKey('customer-services'),
      primary: false,
      padding: const EdgeInsets.all(AppSpacing.lg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const SectionHeader(
            title: 'What do you need help with?',
            subtitle: 'Choose a service category to get started quickly',
          ),
          const SizedBox(height: AppSpacing.md),
          LayoutBuilder(
            builder: (context, constraints) {
              // Fit the longest existing label at the user's text scale.
              final label = TextPainter(
                text: const TextSpan(
                  text: 'Vehicle Assistance',
                  style: AppTextStyles.cardHeading,
                ),
                textDirection: Directionality.of(context),
                textScaler: MediaQuery.textScalerOf(context),
              )..layout();
              final minimum = label.width + AppSpacing.md * 2;
              label.dispose();
              final columns =
                  constraints.maxWidth >= minimum * 2 + AppSpacing.sm ? 2 : 1;
              final width =
                  (constraints.maxWidth - AppSpacing.sm * (columns - 1)) /
                  columns;
              return Wrap(
                spacing: AppSpacing.sm,
                runSpacing: AppSpacing.sm,
                children: [
                  for (final category
                      in CanonicalServiceCategory.canonicalShortcuts)
                    SizedBox(
                      width: width,
                      child: ServiceCategoryCard(
                        category: category,
                        onTap: () => navigateToCreate(
                          categoryPreference: category.canonicalName,
                        ),
                      ),
                    ),
                ],
              );
            },
          ),
          const SizedBox(height: AppSpacing.lg),
          // 3. AI Assistance Option Card
          Material(
            color: Colors.transparent,
            child: InkWell(
              onTap: () => navigateToCreate(categoryPreference: null),
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
                      width: 48,
                      height: 48,
                      padding: const EdgeInsets.all(AppSpacing.xs),
                      decoration: BoxDecoration(
                        color: AppColors.aiSurface,
                        borderRadius: BorderRadius.circular(AppRadius.medium),
                        border: Border.all(color: const Color(0xFFDDD6FE)),
                      ),
                      child: const Center(
                        child: AppImageAsset(
                          assetPath: AppAssets.aiDiagnosisSpark,
                          width: 38,
                          height: 38,
                          fit: BoxFit.contain,
                          fallbackIcon: Icons.auto_awesome_rounded,
                          semanticLabel: 'AssistLK AI Problem Understanding',
                        ),
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
                            'Describe your issue in plain language and let AssistLK AI analyze your problem, identify the right service, and estimate urgency.',
                            style: AppTextStyles.body.copyWith(
                              color: AppColors.textSecondary,
                            ),
                          ),
                          const SizedBox(height: AppSpacing.sm),
                          Row(
                            mainAxisSize: MainAxisSize.min,
                            crossAxisAlignment: CrossAxisAlignment.center,
                            children: [
                              Flexible(
                                child: Text(
                                  'Let AssistLK AI analyze your problem',
                                  style: AppTextStyles.small.copyWith(
                                    color: AppColors.primary,
                                    fontWeight: FontWeight.w600,
                                  ),
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
        ],
      ),
    );
  }
}
