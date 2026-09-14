import 'package:flutter/material.dart';

import '../../../../shared/theme/app_colors.dart';
import '../../../../shared/theme/app_radius.dart';
import '../../../../shared/theme/app_spacing.dart';
import '../../../../shared/theme/app_text_styles.dart';
import '../../../../shared/widgets/app_card.dart';
import '../models/service_request_model.dart';
import 'status_badge.dart';
import 'urgency_chip.dart';
import 'service_request_category_asset.dart';

class ServiceRequestCard extends StatelessWidget {
  final ServiceRequestModel request;
  final VoidCallback? onTap;

  const ServiceRequestCard({super.key, required this.request, this.onTap});

  @override
  Widget build(BuildContext context) {
    final asset = serviceRequestCategoryAsset(request.category);
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(12),
      child: AppCard(
        child: Stack(
          children: [
            if (asset != null)
              Positioned.fill(
                child: IgnorePointer(
                  child: ExcludeSemantics(
                    child: ClipRRect(
                      borderRadius: BorderRadius.circular(AppRadius.large),
                      child: Align(
                        alignment: Alignment.centerRight,
                        child: FractionallySizedBox(
                          widthFactor: 0.33,
                          heightFactor: 1,
                          child: Opacity(
                            opacity: 0.12,
                            child: ShaderMask(
                              blendMode: BlendMode.dstIn,
                              shaderCallback: (bounds) => const LinearGradient(
                                colors: [Colors.transparent, AppColors.surface],
                                stops: [0, 0.8],
                              ).createShader(bounds),
                              child: Image.asset(
                                asset,
                                fit: BoxFit.contain,
                                excludeFromSemantics: true,
                                errorBuilder: (_, _, _) =>
                                    const SizedBox.shrink(),
                              ),
                            ),
                          ),
                        ),
                      ),
                    ),
                  ),
                ),
              ),
            Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                // Top row: Category and Status Badge
                Wrap(
                  alignment: WrapAlignment.spaceBetween,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  spacing: AppSpacing.sm,
                  runSpacing: AppSpacing.sm,
                  children: [
                    Text(
                      request.category.isEmpty
                          ? 'General Request'
                          : request.category,
                      style: AppTextStyles.cardHeading,
                    ),
                    StatusBadge(status: request.status),
                  ],
                ),
                const SizedBox(height: AppSpacing.sm),

                // Description preview
                Text(
                  request.description,
                  style: AppTextStyles.body.copyWith(
                    color: AppColors.textSecondary,
                  ),
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                ),
                const SizedBox(height: AppSpacing.md),

                // Divider or subtle separation
                const Divider(height: 1, color: AppColors.border),
                const SizedBox(height: AppSpacing.sm),

                // Bottom row: Urgency Chip and Created Date
                Wrap(
                  alignment: WrapAlignment.spaceBetween,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  spacing: AppSpacing.xs,
                  runSpacing: AppSpacing.xs,
                  children: [
                    UrgencyChip(urgency: request.urgency),
                    Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        const Icon(
                          Icons.calendar_today_outlined,
                          size: 13,
                          color: AppColors.textSecondary,
                        ),
                        const SizedBox(width: AppSpacing.xs),
                        Flexible(
                          child: Text(
                            _formatDate(request.createdAt),
                            style: AppTextStyles.small,
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  String _formatDate(DateTime date) {
    final year = date.year.toString();
    final month = date.month.toString().padLeft(2, '0');
    final day = date.day.toString().padLeft(2, '0');
    return '$year-$month-$day';
  }
}
