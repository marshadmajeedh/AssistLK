import 'package:flutter/material.dart';

import '../../../../shared/theme/app_spacing.dart';
import 'service_request_category_asset.dart';

/// Decorative context confined to the request summary, independent of status.
class RequestSummaryArtwork extends StatelessWidget {
  final String category;
  final Widget child;
  const RequestSummaryArtwork({
    super.key,
    required this.category,
    required this.child,
  });

  @override
  Widget build(BuildContext context) {
    final asset = category == 'Unclassified'
        ? serviceRequestCategoryAsset(category)
        : null;
    if (asset == null) return child;
    return LayoutBuilder(
      builder: (context, constraints) {
        final wide =
            constraints.maxWidth >=
            400 * MediaQuery.textScalerOf(context).scale(14) / 14;
        final artwork = IgnorePointer(
          child: ExcludeSemantics(
            child: Opacity(
              opacity: 0.65,
              child: Image.asset(
                asset,
                width: wide ? 72 : 48,
                height: wide ? 72 : 48,
                fit: BoxFit.contain,
                excludeFromSemantics: true,
                errorBuilder: (_, _, _) => const SizedBox.shrink(),
              ),
            ),
          ),
        );
        if (wide) {
          return Row(
            children: [
              Expanded(child: child),
              const SizedBox(width: AppSpacing.md),
              artwork,
            ],
          );
        }
        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Align(alignment: Alignment.centerRight, child: artwork),
            const SizedBox(height: AppSpacing.sm),
            child,
          ],
        );
      },
    );
  }
}
