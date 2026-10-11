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
  final bool isExpired;

  const ReadyForMatchingSection({
    super.key,
    this.onProceedToMatching,
    this.isLoading = false,
    this.isExpired = false,
  });

  @override
  Widget build(BuildContext context) {
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: [
          // Ready for Matching illustration
          AppImageAsset(
            assetPath: AppAssets.readyForMatching,
            width: 88,
            height: 88,
            fit: BoxFit.contain,
            fallbackIcon: isExpired ? Icons.schedule_rounded : Icons.verified_rounded,
            semanticLabel: isExpired ? 'Matching window expired' : 'Ready for matching',
          ),
          const SizedBox(height: AppSpacing.sm + 2),

          // Headline
          Text(
            isExpired ? 'Matching Window Expired' : 'Request Understood',
            style: AppTextStyles.sectionHeading,
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: AppSpacing.xs),

          // Subtitle / Ready for matching message
          Text(
            isExpired
                ? 'The matching window for this service request has expired.'
                : 'Your request has been clearly analyzed and is ready for provider matching.',
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
              color: isExpired ? const Color(0xFFFFFBEB) : const Color(0xFFF0FDF4),
              borderRadius: BorderRadius.circular(AppRadius.pill),
              border: Border.all(
                color: isExpired ? const Color(0xFFFDE68A) : const Color(0xFFBBF7D0),
              ),
            ),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(
                  isExpired ? Icons.schedule_rounded : Icons.verified_rounded,
                  size: 16,
                  color: isExpired ? AppColors.warning : AppColors.success,
                ),
                const SizedBox(width: AppSpacing.xs + 2),
                Flexible(
                  child: Text(
                    isExpired ? 'Matching window expired' : 'Ready for provider matching',
                    style: TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w600,
                      color: isExpired ? AppColors.warning : AppColors.success,
                    ),
                  ),
                ),
              ],
            ),
          ),

          if (isExpired) ...[
            const SizedBox(height: AppSpacing.md),
            Text(
              'Create a new service request if you still need help.',
              style: AppTextStyles.small.copyWith(
                color: AppColors.textSecondary,
                fontStyle: FontStyle.italic,
              ),
              textAlign: TextAlign.center,
            ),
          ],

          if (!isExpired && onProceedToMatching != null) ...[
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
