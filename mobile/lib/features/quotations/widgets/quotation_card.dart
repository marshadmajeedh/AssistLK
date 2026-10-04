import 'package:flutter/material.dart';

import '../../../shared/theme/app_colors.dart';
import '../../../shared/theme/app_spacing.dart';
import '../../../shared/theme/app_text_styles.dart';
import '../../../shared/widgets/app_card.dart';
import '../models/quotation_model.dart';
import 'quotation_status_badge.dart';

class QuotationCard extends StatelessWidget {
  final QuotationModel quotation;
  final VoidCallback? onTap;

  const QuotationCard({
    super.key,
    required this.quotation,
    this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(16),
      child: AppCard(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  child: Text(
                    'Quotation #${quotation.id}',
                    style: AppTextStyles.cardHeading,
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
                const SizedBox(width: AppSpacing.sm),
                QuotationStatusBadge(status: quotation.status),
              ],
            ),
            const SizedBox(height: AppSpacing.xs),
            Text(
              quotation.items.length == 1
                  ? '1 line item'
                  : '${quotation.items.length} line items',
              style: AppTextStyles.small.copyWith(
                color: AppColors.textSecondary,
              ),
            ),
            const SizedBox(height: AppSpacing.sm),
            const Divider(height: 1, color: AppColors.border),
            const SizedBox(height: AppSpacing.sm),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  'Total',
                  style: AppTextStyles.small.copyWith(
                    color: AppColors.textSecondary,
                  ),
                ),
                Text(
                  'Rs. ${quotation.totalAmount.toStringAsFixed(2)}',
                  style: AppTextStyles.cardHeading.copyWith(
                    color: AppColors.primary,
                  ),
                ),
              ],
            ),
            if (quotation.canApprove) ...[
              const SizedBox(height: AppSpacing.sm),
              Row(
                children: [
                  Icon(
                    Icons.touch_app_rounded,
                    size: 14,
                    color: AppColors.warning,
                  ),
                  const SizedBox(width: 4),
                  Text(
                    'Tap to review and respond',
                    style: AppTextStyles.small.copyWith(
                      color: AppColors.warning,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ],
              ),
            ],
          ],
        ),
      ),
    );
  }
}