import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../shared/theme/app_colors.dart';
import '../shared/theme/app_radius.dart';
import '../shared/theme/app_spacing.dart';
import '../shared/theme/app_text_styles.dart';

class CustomerBottomNavigation extends StatelessWidget {
  final int selectedIndex;
  final ValueChanged<int> onDestinationSelected;
  static const labels = ['Home', 'Services', 'Activity', 'Account'];
  static const _icons = [
    Icons.home_outlined,
    Icons.home_repair_service_outlined,
    Icons.assignment_outlined,
    Icons.person_outline_rounded,
  ];
  static const _selectedIcons = [
    Icons.home_rounded,
    Icons.home_repair_service_rounded,
    Icons.assignment_rounded,
    Icons.person_rounded,
  ];

  const CustomerBottomNavigation({
    super.key,
    required this.selectedIndex,
    required this.onDestinationSelected,
  });

  @override
  Widget build(BuildContext context) => SafeArea(
    top: false,
    child: Padding(
      padding: const EdgeInsets.fromLTRB(
        AppSpacing.lg,
        AppSpacing.sm,
        AppSpacing.lg,
        AppSpacing.sm + AppSpacing.xs,
      ),
      child: Material(
        key: const Key('floating_navigation_surface'),
        color: AppColors.primaryDark,
        elevation: 4,
        shadowColor: AppColors.primaryDark.withValues(alpha: 0.2),
        borderRadius: BorderRadius.circular(AppRadius.large * 2),
        child: Padding(
          padding: const EdgeInsets.all(AppSpacing.xs),
          child: LayoutBuilder(
            builder: (context, constraints) {
              final style = AppTextStyles.small.copyWith(
                fontWeight: FontWeight.w700,
              );
              var labelWidth = 0.0;
              var labelHeight = 0.0;
              for (final label in labels) {
                final painter = TextPainter(
                  text: TextSpan(
                    text: label,
                    style: DefaultTextStyle.of(context).style.merge(style),
                  ),
                  textScaler: MediaQuery.textScalerOf(context),
                  textDirection: Directionality.of(context),
                )..layout();
                labelWidth = math.max(labelWidth, painter.width);
                labelHeight = math.max(labelHeight, painter.height);
                painter.dispose();
              }
              final minimum = math.max(48.0, labelWidth + AppSpacing.sm);
              final columns =
                  constraints.maxWidth >= minimum * 4 + AppSpacing.xs * 3
                  ? 4
                  : 2;
              final width =
                  (constraints.maxWidth - AppSpacing.xs * (columns - 1)) /
                  columns;
              final height = math.max(
                64.0,
                24 + AppSpacing.xs + labelHeight + AppSpacing.md,
              );
              return Wrap(
                spacing: AppSpacing.xs,
                runSpacing: AppSpacing.xs,
                children: [
                  for (var index = 0; index < labels.length; index++)
                    SizedBox(
                      width: width,
                      height: height,
                      child: Semantics(
                        key: ValueKey('customer_navigation_${labels[index]}'),
                        label: labels[index],
                        button: true,
                        selected: selectedIndex == index,
                        onTap: () => onDestinationSelected(index),
                        child: ExcludeSemantics(
                          child: AnimatedContainer(
                            key: ValueKey('customer_navigation_pill_$index'),
                            duration: const Duration(milliseconds: 200),
                            curve: Curves.easeInOut,
                            decoration: BoxDecoration(
                              color: selectedIndex == index
                                  ? AppColors.primary
                                  : Colors.transparent,
                              borderRadius: BorderRadius.circular(
                                AppRadius.pill,
                              ),
                            ),
                            child: Material(
                              color: Colors.transparent,
                              child: InkWell(
                                borderRadius: BorderRadius.circular(
                                  AppRadius.pill,
                                ),
                                onTap: () => onDestinationSelected(index),
                                child: Column(
                                  mainAxisAlignment: MainAxisAlignment.center,
                                  children: [
                                    Icon(
                                      selectedIndex == index
                                          ? _selectedIcons[index]
                                          : _icons[index],
                                      size: 24,
                                      color: selectedIndex == index
                                          ? AppColors.surface
                                          : AppColors.border,
                                    ),
                                    const SizedBox(height: AppSpacing.xs),
                                    Text(
                                      labels[index],
                                      maxLines: 1,
                                      softWrap: false,
                                      style: style.copyWith(
                                        color: selectedIndex == index
                                            ? AppColors.surface
                                            : AppColors.border,
                                        fontWeight: selectedIndex == index
                                            ? FontWeight.w700
                                            : FontWeight.w500,
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                            ),
                          ),
                        ),
                      ),
                    ),
                ],
              );
            },
          ),
        ),
      ),
    ),
  );
}
