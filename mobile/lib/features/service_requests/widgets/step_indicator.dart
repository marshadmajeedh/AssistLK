import 'package:flutter/material.dart';

import '../../../../shared/theme/app_colors.dart';
import '../../../../shared/theme/app_spacing.dart';
import '../../../../shared/theme/app_text_styles.dart';

class StepIndicator extends StatelessWidget {
  final int currentStep;
  final List<String> steps;

  const StepIndicator({
    super.key,
    required this.currentStep,
    this.steps = const ['Details', 'Location', 'Review'],
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: AppSpacing.sm,
        vertical: AppSpacing.md,
      ),
      child: LayoutBuilder(
        builder: (context, constraints) {
          var requiredWidth = (steps.length - 1) * AppSpacing.sm;
          for (final step in steps) {
            final label = TextPainter(
              text: TextSpan(
                text: step,
                style: AppTextStyles.small.copyWith(
                  fontWeight: FontWeight.w600,
                ),
              ),
              textDirection: Directionality.of(context),
              textScaler: MediaQuery.textScalerOf(context),
            )..layout();
            requiredWidth += 24 + AppSpacing.xs + label.width;
            label.dispose();
          }
          if (constraints.maxWidth < requiredWidth) {
            return Wrap(
              spacing: AppSpacing.sm,
              runSpacing: AppSpacing.sm,
              children: [
                for (int i = 0; i < steps.length; i++) _buildStepItem(i),
              ],
            );
          }
          return Row(
            children: [
              for (int i = 0; i < steps.length; i++) ...[
                _buildStepItem(i),
                if (i < steps.length - 1) _buildDivider(i),
              ],
            ],
          );
        },
      ),
    );
  }

  Widget _buildStepItem(int stepIndex) {
    final isCompleted = stepIndex < currentStep;
    final isActive = stepIndex == currentStep;

    Color circleColor;
    Color iconOrTextColor;
    Widget content;

    if (isCompleted) {
      circleColor = AppColors.primary;
      iconOrTextColor = Colors.white;
      content = Icon(Icons.check, size: 14, color: iconOrTextColor);
    } else if (isActive) {
      circleColor = AppColors.primary;
      iconOrTextColor = Colors.white;
      content = Text(
        '${stepIndex + 1}',
        style: TextStyle(
          color: iconOrTextColor,
          fontSize: 12,
          fontWeight: FontWeight.bold,
        ),
      );
    } else {
      circleColor = AppColors.border;
      iconOrTextColor = AppColors.textSecondary;
      content = Text(
        '${stepIndex + 1}',
        style: TextStyle(
          color: iconOrTextColor,
          fontSize: 12,
          fontWeight: FontWeight.w500,
        ),
      );
    }

    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          width: 24,
          height: 24,
          decoration: BoxDecoration(color: circleColor, shape: BoxShape.circle),
          child: Center(child: content),
        ),
        const SizedBox(width: AppSpacing.xs),
        Text(
          steps[stepIndex],
          style: AppTextStyles.small.copyWith(
            fontWeight: isActive ? FontWeight.w600 : FontWeight.w400,
            color: isActive ? AppColors.textPrimary : AppColors.textSecondary,
          ),
        ),
      ],
    );
  }

  Widget _buildDivider(int stepIndex) {
    final isCompleted = stepIndex < currentStep;

    return Expanded(
      child: Container(
        height: 2,
        margin: const EdgeInsets.symmetric(horizontal: AppSpacing.xs),
        color: isCompleted ? AppColors.primary : AppColors.border,
      ),
    );
  }
}
