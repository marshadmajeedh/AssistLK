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
    final asset = serviceRequestCategoryAsset(category);
    if (asset == null) return child;
    return LayoutBuilder(
      builder: (context, constraints) {
        final wide = constraints.maxWidth >= 480;
        final artworkSize = wide ? 72.0 : 48.0;
        final artwork = IgnorePointer(
          child: ExcludeSemantics(
            child: Opacity(
              opacity: 0.65,
              child: Image.asset(
                asset,
                width: artworkSize,
                height: artworkSize,
                fit: BoxFit.contain,
                excludeFromSemantics: true,
                errorBuilder: (_, _, _) => const SizedBox.shrink(),
              ),
            ),
          ),
        );
        return Row(
          crossAxisAlignment: CrossAxisAlignment.center,
          children: [
            Expanded(child: child),
            const SizedBox(width: AppSpacing.md),
            Padding(
              padding: const EdgeInsets.only(right: AppSpacing.xs),
              child: artwork,
            ),
          ],
        );
      },
    );
  }
}
