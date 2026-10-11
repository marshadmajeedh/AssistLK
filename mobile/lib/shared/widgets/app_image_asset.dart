import 'package:flutter/material.dart';

import '../theme/app_colors.dart';

/// A safe, responsive local asset image widget.
///
/// Ensures explicit dimensions, safe [BoxFit], optional semantic label,
/// and graceful fallback to an [Icon] via [errorBuilder] without any
/// network calls.
class AppImageAsset extends StatelessWidget {
  final String assetPath;
  final double? width;
  final double? height;
  final BoxFit fit;
  final IconData? fallbackIcon;
  final String? semanticLabel;
  final BorderRadiusGeometry? borderRadius;

  const AppImageAsset({
    super.key,
    required this.assetPath,
    this.width,
    this.height,
    this.fit = BoxFit.contain,
    this.fallbackIcon,
    this.semanticLabel,
    this.borderRadius,
  });

  @override
  Widget build(BuildContext context) {
    Widget imageWidget = Image.asset(
      assetPath,
      width: width,
      height: height,
      fit: fit,
      semanticLabel: semanticLabel,
      errorBuilder: (context, error, stackTrace) {
        return _buildFallback();
      },
    );

    if (borderRadius != null) {
      imageWidget = ClipRRect(
        borderRadius: borderRadius!,
        child: imageWidget,
      );
    }

    return imageWidget;
  }

  Widget _buildFallback() {
    final double iconSize = (width != null && height != null)
        ? (width! < height! ? width! * 0.5 : height! * 0.5).clamp(16.0, 48.0)
        : 24.0;

    return Container(
      width: width,
      height: height,
      color: AppColors.background,
      alignment: Alignment.center,
      child: Icon(
        fallbackIcon ?? Icons.image_outlined,
        color: AppColors.disabled,
        size: iconSize,
      ),
    );
  }
}
