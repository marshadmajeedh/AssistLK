import 'package:flutter/material.dart';

import '../../../../shared/theme/app_colors.dart';
import '../../../../shared/theme/app_radius.dart';
import '../../../../shared/theme/app_spacing.dart';
import '../models/service_request_status.dart';

class StatusBadge extends StatelessWidget {
  final ServiceRequestStatus status;

  const StatusBadge({super.key, required this.status});

  @override
  Widget build(BuildContext context) {
    final colors = _getStatusColors(status);

    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.sm + 2,
        vertical: AppSpacing.xs,
      ),
      decoration: BoxDecoration(
        color: colors.background,
        borderRadius: BorderRadius.circular(AppRadius.pill),
        border: Border.all(color: colors.border, width: 1),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            width: 6,
            height: 6,
            decoration: BoxDecoration(
              color: colors.foreground,
              shape: BoxShape.circle,
            ),
          ),
          const SizedBox(width: AppSpacing.xs + 2),
          Flexible(
            child: Text(
              status.displayName,
              style: TextStyle(
                fontSize: 12,
                fontWeight: FontWeight.w600,
                color: colors.foreground,
                height: 1.2,
              ),
            ),
          ),
        ],
      ),
    );
  }

  _BadgeColors _getStatusColors(ServiceRequestStatus status) {
    switch (status) {
      case ServiceRequestStatus.created:
        return const _BadgeColors(
          background: Color(0xFFEFF6FF),
          border: Color(0xFFBFDBFE),
          foreground: AppColors.primary,
        );
      case ServiceRequestStatus.analyzing:
        return const _BadgeColors(
          background: Color(0xFFF5F3FF),
          border: Color(0xFFDDD6FE),
          foreground: Color(0xFF6D28D9),
        );
      case ServiceRequestStatus.awaitingInformation:
        return const _BadgeColors(
          background: Color(0xFFFFFBEB),
          border: Color(0xFFFDE68A),
          foreground: AppColors.warning,
        );
      case ServiceRequestStatus.analyzed:
        return const _BadgeColors(
          background: Color(0xFFF0FDFA),
          border: Color(0xFF99F6E4),
          foreground: AppColors.secondary,
        );
      case ServiceRequestStatus.readyForMatching:
        return const _BadgeColors(
          background: Color(0xFFF0FDF4),
          border: Color(0xFFBBF7D0),
          foreground: AppColors.success,
        );
      case ServiceRequestStatus.cancelled:
        return const _BadgeColors(
          background: Color(0xFFF3F4F6),
          border: AppColors.border,
          foreground: AppColors.textSecondary,
        );
    }
  }
}

class _BadgeColors {
  final Color background;
  final Color border;
  final Color foreground;

  const _BadgeColors({
    required this.background,
    required this.border,
    required this.foreground,
  });
}
