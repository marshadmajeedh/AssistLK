import 'package:flutter/material.dart';

import '../../../../shared/theme/app_assets.dart';
import '../../../../shared/theme/app_colors.dart';
import '../../../../shared/theme/app_radius.dart';
import '../../../../shared/theme/app_spacing.dart';
import '../../../../shared/theme/app_text_styles.dart';
import '../../../../shared/widgets/app_button.dart';
import '../../../../shared/widgets/app_card.dart';
import '../../../../shared/widgets/app_image_asset.dart';

class ReadyForMatchingSection extends StatelessWidget {
  final VoidCallback? onProceedToMatching;
  final bool isLoading;

  const ReadyForMatchingSection({
    super.key,
    this.onProceedToMatching,
    this.isLoading = false,
  });

  @override
  Widget build(BuildContext context) {
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: [
          // Ready for Matching illustration
          const AppImageAsset(
            assetPath: AppAssets.readyForMatching,
            width: 88,
            height: 88,
            fit: BoxFit.contain,
            fallbackIcon: Icons.verified_rounded,
            semanticLabel: 'Ready for matching',
          ),
          const SizedBox(height: AppSpacing.sm + 2),

          // Headline
          const Text(
            'Request Understood',
            style: AppTextStyles.sectionHeading,
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: AppSpacing.xs),

          // Subtitle / Ready for matching message
          Text(
            'Your request has been clearly analyzed and is ready for provider matching.',
            style: AppTextStyles.body.copyWith(color: AppColors.textSecondary),
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: AppSpacing.md),

          // Status confirmation chip
          Container(
            padding: const EdgeInsets.symmetric(
              horizontal: AppSpacing.md,
              vertical: AppSpacing.xs + 2,
            ),
            decoration: BoxDecoration(
              color: const Color(0xFFF0FDF4),
              borderRadius: BorderRadius.circular(AppRadius.pill),
              border: Border.all(color: const Color(0xFFBBF7D0)),
            ),
            child: const Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(
                  Icons.verified_rounded,
                  size: 16,
                  color: AppColors.success,
                ),
                SizedBox(width: AppSpacing.xs + 2),
                Flexible(
                  child: Text(
                    'Ready for provider matching',
                    style: TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w600,
                      color: AppColors.success,
                    ),
                  ),
                ),
              ],
            ),
          ),

          if (onProceedToMatching != null) ...[
            const SizedBox(height: AppSpacing.lg),
            AppButton(
              text: 'Find Matching Providers',
              onPressed: onProceedToMatching,
              isLoading: isLoading,
            ),
          ],
        ],
      ),
    );
  }
}
