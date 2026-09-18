import 'package:flutter/material.dart';

import '../theme/app_colors.dart';
import '../theme/app_radius.dart';
import '../theme/app_spacing.dart';
import '../theme/app_text_styles.dart';
import 'app_button.dart';
import 'app_image_asset.dart';

/// A reusable empty state presentation card.
///
/// Displays an illustration or icon, a prominent heading, descriptive copy,
/// and an optional call-to-action button with finite layout constraints.
class EmptyStateCard extends StatelessWidget {
  final String title;
  final String description;
  final String? imageAsset;
  final IconData? icon;
  final String? actionText;
  final VoidCallback? onAction;
  final double actionWidth;

  const EmptyStateCard({
    super.key,
    required this.title,
    required this.description,
    this.imageAsset,
    this.icon,
    this.actionText,
    this.onAction,
    this.actionWidth = 200,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.lg,
        vertical: 36,
      ),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppRadius.large),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        crossAxisAlignment: CrossAxisAlignment.center,
        children: [
          if (imageAsset != null) ...[
            AppImageAsset(
              assetPath: imageAsset!,
              width: 120,
              height: 120,
              fit: BoxFit.contain,
              fallbackIcon: icon ?? Icons.assignment_outlined,
              semanticLabel: title,
            ),
            const SizedBox(height: AppSpacing.md),
          ] else if (icon != null) ...[
            Container(
              padding: const EdgeInsets.all(AppSpacing.md),
              decoration: BoxDecoration(
                color: AppColors.primary.withValues(alpha: 0.08),
                shape: BoxShape.circle,
              ),
              child: Icon(
                icon,
                size: 40,
                color: AppColors.primary,
              ),
            ),
            const SizedBox(height: AppSpacing.md),
          ],
          Text(
            title,
            style: AppTextStyles.cardHeading,
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: AppSpacing.xs),
          ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 360),
            child: Text(
              description,
              style: AppTextStyles.body.copyWith(
                color: AppColors.textSecondary,
              ),
              textAlign: TextAlign.center,
            ),
          ),
          if (actionText != null && onAction != null) ...[
            const SizedBox(height: AppSpacing.lg),
            SizedBox(
              width: actionWidth,
              child: AppButton(
                text: actionText!,
                onPressed: onAction,
              ),
            ),
          ],
        ],
      ),
    );
  }
}
