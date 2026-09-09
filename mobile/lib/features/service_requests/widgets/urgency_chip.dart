import 'package:flutter/material.dart';

import '../../../../shared/theme/app_colors.dart';
import '../../../../shared/theme/app_radius.dart';
import '../../../../shared/theme/app_spacing.dart';
import '../models/service_request_urgency.dart';

class UrgencyChip extends StatelessWidget {
  final ServiceRequestUrgency urgency;

  const UrgencyChip({
    super.key,
    required this.urgency,
  });

  @override
  Widget build(BuildContext context) {
    final colors = _getUrgencyColors(urgency);
    final icon = _getUrgencyIcon(urgency);

    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.sm,
        vertical: AppSpacing.xs,
      ),
      decoration: BoxDecoration(
        color: colors.background,
        borderRadius: BorderRadius.circular(AppRadius.small),
        border: Border.all(
          color: colors.border,
          width: 1,
        ),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(
            icon,
            size: 14,
            color: colors.foreground,
          ),
          const SizedBox(width: AppSpacing.xs),
          Text(
            urgency.displayName,
            style: TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w600,
              color: colors.foreground,
              height: 1.2,
            ),
          ),
        ],
      ),
    );
  }

  _UrgencyColors _getUrgencyColors(ServiceRequestUrgency urgency) {
    switch (urgency) {
      case ServiceRequestUrgency.unknown:
        return const _UrgencyColors(
          background: Color(0xFFF3F4F6),
          border: AppColors.border,
          foreground: AppColors.textSecondary,
        );
      case ServiceRequestUrgency.low:
        return const _UrgencyColors(
          background: Color(0xFFEFF6FF),
          border: Color(0xFFBFDBFE),
          foreground: AppColors.primary,
        );
      case ServiceRequestUrgency.medium:
        return const _UrgencyColors(
          background: Color(0xFFFFFBEB),
          border: Color(0xFFFDE68A),
          foreground: AppColors.warning,
        );
      case ServiceRequestUrgency.high:
        return const _UrgencyColors(
          background: Color(0xFFFFF7ED),
          border: Color(0xFFFED7AA),
          foreground: Color(0xFFEA580C),
        );
      case ServiceRequestUrgency.critical:
        return const _UrgencyColors(
          background: Color(0xFFFEF2F2),
          border: Color(0xFFFECACA),
          foreground: AppColors.error,
        );
    }
  }

  IconData _getUrgencyIcon(ServiceRequestUrgency urgency) {
    switch (urgency) {
      case ServiceRequestUrgency.unknown:
        return Icons.help_outline_rounded;
      case ServiceRequestUrgency.low:
        return Icons.arrow_downward_rounded;
      case ServiceRequestUrgency.medium:
        return Icons.remove_rounded;
      case ServiceRequestUrgency.high:
        return Icons.arrow_upward_rounded;
      case ServiceRequestUrgency.critical:
        return Icons.priority_high_rounded;
    }
  }
}

class _UrgencyColors {
  final Color background;
  final Color border;
  final Color foreground;

  const _UrgencyColors({
    required this.background,
    required this.border,
    required this.foreground,
  });
}
