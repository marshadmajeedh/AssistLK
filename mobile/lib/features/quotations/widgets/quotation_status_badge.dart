import 'package:flutter/material.dart';

import '../../../shared/theme/app_colors.dart';
import '../../../shared/theme/app_radius.dart';
import '../../../shared/theme/app_spacing.dart';
import '../models/quotation_status.dart';

class QuotationStatusBadge extends StatelessWidget {
  final QuotationStatus status;

  const QuotationStatusBadge({super.key, required this.status});

  @override
  Widget build(BuildContext context) {
    final (bg, fg, icon) = _colorsFor(status);
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.sm,
        vertical: 4,
      ),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: BorderRadius.circular(AppRadius.small),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 14, color: fg),
          const SizedBox(width: 4),
          Text(
            status.displayLabel,
            style: TextStyle(
              color: fg,
              fontSize: 12,
              fontWeight: FontWeight.w600,
            ),
          ),
        ],
      ),
    );
  }

  (Color, Color, IconData) _colorsFor(QuotationStatus s) {
    switch (s) {
      case QuotationStatus.draft:
        return (
          AppColors.border.withValues(alpha: 0.4),
          AppColors.textSecondary,
          Icons.edit_note_rounded,
        );
      case QuotationStatus.sent:
        return (
          AppColors.primarySurface,
          AppColors.primary,
          Icons.send_rounded,
        );
      case QuotationStatus.waitingForCustomerApproval:
        return (
          AppColors.warningSurface,
          AppColors.warning,
          Icons.hourglass_top_rounded,
        );
      case QuotationStatus.approved:
        return (
          AppColors.successSurface,
          AppColors.success,
          Icons.check_circle_rounded,
        );
      case QuotationStatus.rejected:
        return (
          const Color(0xFFFEF2F2),
          AppColors.error,
          Icons.cancel_rounded,
        );
      case QuotationStatus.expired:
        return (
          AppColors.border.withValues(alpha: 0.4),
          AppColors.textSecondary,
          Icons.timer_off_rounded,
        );
    }
  }
}