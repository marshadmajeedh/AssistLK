import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../../../../shared/theme/app_spacing.dart';
import '../../../../shared/theme/app_text_styles.dart';
import '../models/canonical_service_category.dart';
import 'service_category_card.dart';

class ServiceCategoryShortcuts extends StatelessWidget {
  final ValueChanged<CanonicalServiceCategory> onSelected;
  const ServiceCategoryShortcuts({super.key, required this.onSelected});

  @override
  Widget build(BuildContext context) => LayoutBuilder(
    builder: (context, constraints) {
      final categories = CanonicalServiceCategory.canonicalShortcuts;
      TextPainter measure(String text, TextStyle style, double width) =>
          TextPainter(
            text: TextSpan(
              text: text,
              style: DefaultTextStyle.of(context).style.merge(style),
            ),
            textDirection: Directionality.of(context),
            textScaler: MediaQuery.textScalerOf(context),
          )..layout(maxWidth: width);

      double contentWidth(int columns) => math.max(
        1,
        (constraints.maxWidth - AppSpacing.sm * (columns - 1)) / columns -
            2 * (AppSpacing.md + 1),
      );
      // Keep two columns when every label fits comfortably. At larger text
      // scales, a single column preserves the full category wording.
      var columns = 2;
      for (final category in categories) {
        final title = measure(
          category.displayName,
          AppTextStyles.cardHeading,
          contentWidth(2),
        );
        final description = measure(
          category.description,
          AppTextStyles.small,
          contentWidth(2),
        );
        if (title.computeLineMetrics().length > 2 ||
            description.computeLineMetrics().length > 3) {
          columns = 1;
        }
        title.dispose();
        description.dispose();
      }
      var titleHeight = 0.0;
      var descriptionHeight = 0.0;
      for (final category in categories) {
        final title = measure(
          category.displayName,
          AppTextStyles.cardHeading,
          contentWidth(columns),
        );
        final description = measure(
          category.description,
          AppTextStyles.small,
          contentWidth(columns),
        );
        titleHeight = math.max(titleHeight, title.height.ceilToDouble());
        descriptionHeight = math.max(
          descriptionHeight,
          description.height.ceilToDouble(),
        );
        title.dispose();
        description.dispose();
      }
      return GridView.builder(
        shrinkWrap: true,
        primary: false,
        physics: const NeverScrollableScrollPhysics(),
        padding: EdgeInsets.zero,
        itemCount: categories.length,
        gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
          crossAxisCount: columns,
          crossAxisSpacing: AppSpacing.sm,
          mainAxisSpacing: AppSpacing.sm,
          mainAxisExtent:
              2 * (AppSpacing.md + 1) +
              48 +
              AppSpacing.sm +
              titleHeight +
              AppSpacing.xs +
              descriptionHeight,
        ),
        itemBuilder: (context, index) => ServiceCategoryCard(
          category: categories[index],
          titleHeight: titleHeight,
          descriptionHeight: descriptionHeight,
          onTap: () => onSelected(categories[index]),
        ),
      );
    },
  );
}
