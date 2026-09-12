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
      // Fit the longest existing label at the user's text scale.
      final label = TextPainter(
        text: const TextSpan(
          text: 'Vehicle Assistance',
          style: AppTextStyles.cardHeading,
        ),
        textDirection: Directionality.of(context),
        textScaler: MediaQuery.textScalerOf(context),
      )..layout();
      final minimum = label.width + AppSpacing.md * 2;
      label.dispose();
      final columns = constraints.maxWidth >= minimum * 2 + AppSpacing.sm
          ? 2
          : 1;
      final width =
          (constraints.maxWidth - AppSpacing.sm * (columns - 1)) / columns;
      return Wrap(
        spacing: AppSpacing.sm,
        runSpacing: AppSpacing.sm,
        children: [
          for (final category in CanonicalServiceCategory.canonicalShortcuts)
            SizedBox(
              width: width,
              child: ServiceCategoryCard(
                category: category,
                onTap: () => onSelected(category),
              ),
            ),
        ],
      );
    },
  );
}
