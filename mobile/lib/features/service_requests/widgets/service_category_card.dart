import 'package:flutter/material.dart';

import '../../../../shared/theme/app_assets.dart';
import '../../../../shared/theme/app_colors.dart';
import '../../../../shared/theme/app_radius.dart';
import '../../../../shared/theme/app_spacing.dart';
import '../../../../shared/theme/app_text_styles.dart';
import '../../../../shared/widgets/app_image_asset.dart';
import '../models/canonical_service_category.dart';

class ServiceCategoryCard extends StatelessWidget {
  final CanonicalServiceCategory category;
  final VoidCallback onTap;
  final bool isSelected;

  const ServiceCategoryCard({
    super.key,
    required this.category,
    required this.onTap,
    this.isSelected = false,
  });

  String _getAssetPath() {
    switch (category.canonicalName) {
      case 'Plumbing':
        return AppAssets.plumbingService;
      case 'Electrical':
        return AppAssets.electricalService;
      case 'Vehicle Repair':
        return AppAssets.vehicleService;
      case 'Appliance Repair':
        return AppAssets.applianceService;
      default:
        return AppAssets.plumbingService;
    }
  }

  Color _getCategoryTint() {
    switch (category.canonicalName) {
      case 'Plumbing':
        return AppColors.plumbingTint;
      case 'Electrical':
        return AppColors.electricalTint;
      case 'Vehicle Repair':
        return AppColors.vehicleTint;
      case 'Appliance Repair':
        return AppColors.applianceTint;
      default:
        return AppColors.primarySurface;
    }
  }

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(AppRadius.large),
        child: Container(
          padding: const EdgeInsets.all(AppSpacing.md),
          decoration: BoxDecoration(
            color: isSelected
                ? AppColors.primary.withValues(alpha: 0.05)
                : AppColors.surface,
            borderRadius: BorderRadius.circular(AppRadius.large),
            border: Border.all(
              color: isSelected ? AppColors.primary : AppColors.border,
              width: isSelected ? 2 : 1,
            ),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              Container(
                width: 48,
                height: 48,
                padding: const EdgeInsets.all(AppSpacing.xs),
                decoration: BoxDecoration(
                  color: _getCategoryTint(),
                  borderRadius: BorderRadius.circular(AppRadius.medium),
                ),
                child: Center(
                  child: AppImageAsset(
                    assetPath: _getAssetPath(),
                    width: 38,
                    height: 38,
                    fit: BoxFit.contain,
                    fallbackIcon: category.icon,
                    semanticLabel: category.displayName,
                  ),
                ),
              ),
              const SizedBox(height: AppSpacing.sm),
              Text(
                category.displayName,
                style: AppTextStyles.cardHeading.copyWith(
                  fontWeight: FontWeight.w600,
                ),
              ),
              const SizedBox(height: AppSpacing.xs),
              Text(
                category.description,
                style: AppTextStyles.small.copyWith(
                  color: AppColors.textSecondary,
                ),
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
