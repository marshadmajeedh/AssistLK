import 'dart:math' as math;
import 'package:flutter/material.dart';

import '../theme/app_colors.dart';
import '../theme/app_radius.dart';

/// A reusable Flutter widget that renders a subtle rotating border trail
/// around its [child], styled using AssistLK navy and light-blue theme colors.
///
/// Automatically falls back to a static border when reduced motion /
/// [MediaQueryData.disableAnimations] is active or when running in widget test environments.
class AnimatedBorderTrail extends StatefulWidget {
  final Widget child;
  final BorderRadius? borderRadius;
  final double borderWidth;
  final Duration duration;
  final Color? baseBorderColor;
  final Color? trailColor;
  final Color? trailAccentColor;
  final bool? animate;

  const AnimatedBorderTrail({
    super.key,
    required this.child,
    this.borderRadius,
    this.borderWidth = 1.5,
    this.duration = const Duration(seconds: 9),
    this.baseBorderColor,
    this.trailColor,
    this.trailAccentColor,
    this.animate,
  });

  @override
  State<AnimatedBorderTrail> createState() => _AnimatedBorderTrailState();
}

class _AnimatedBorderTrailState extends State<AnimatedBorderTrail>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(vsync: this, duration: widget.duration);
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    _updateAnimationState();
  }

  @override
  void didUpdateWidget(covariant AnimatedBorderTrail oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.duration != widget.duration) {
      _controller.duration = widget.duration;
    }
    if (oldWidget.animate != widget.animate) {
      _updateAnimationState();
    }
  }

  bool _shouldAnimate() {
    if (widget.animate != null) return widget.animate!;
    final mediaDisable =
        MediaQuery.maybeOf(context)?.disableAnimations ?? false;
    if (mediaDisable) return false;
    if (WidgetsBinding.instance.runtimeType.toString().contains('Test')) {
      return false;
    }
    return true;
  }

  void _updateAnimationState() {
    if (_shouldAnimate()) {
      if (!_controller.isAnimating) {
        _controller.repeat();
      }
    } else {
      if (_controller.isAnimating) {
        _controller.stop();
      }
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final effectiveRadius =
        widget.borderRadius ?? BorderRadius.circular(AppRadius.medium);
    final effectiveBaseColor =
        widget.baseBorderColor ?? AppColors.primary.withValues(alpha: 0.18);
    final effectiveTrailColor = widget.trailColor ?? AppColors.primary;
    final effectiveAccentColor =
        widget.trailAccentColor ?? const Color(0xFF60A5FA);

    final isAnimated = _shouldAnimate();

    if (!isAnimated) {
      return DecoratedBox(
        decoration: BoxDecoration(
          borderRadius: effectiveRadius,
          border: Border.all(
            color: effectiveBaseColor,
            width: widget.borderWidth,
          ),
        ),
        child: widget.child,
      );
    }

    return RepaintBoundary(
      child: AnimatedBuilder(
        animation: _controller,
        builder: (context, child) {
          return CustomPaint(
            painter: _BorderTrailPainter(
              progress: _controller.value,
              borderRadius: effectiveRadius,
              borderWidth: widget.borderWidth,
              baseBorderColor: effectiveBaseColor,
              trailColor: effectiveTrailColor,
              trailAccentColor: effectiveAccentColor,
            ),
            child: child,
          );
        },
        child: widget.child,
      ),
    );
  }
}

class _BorderTrailPainter extends CustomPainter {
  final double progress;
  final BorderRadius borderRadius;
  final double borderWidth;
  final Color baseBorderColor;
  final Color trailColor;
  final Color trailAccentColor;

  _BorderTrailPainter({
    required this.progress,
    required this.borderRadius,
    required this.borderWidth,
    required this.baseBorderColor,
    required this.trailColor,
    required this.trailAccentColor,
  });

  @override
  void paint(Canvas canvas, Size size) {
    if (size.isEmpty) return;
    final rect = Offset.zero & size;
    final rrect = borderRadius.toRRect(rect).deflate(borderWidth / 2);

    // Subtle base border
    final basePaint = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = borderWidth
      ..color = baseBorderColor;
    canvas.drawRRect(rrect, basePaint);

    // Subtle moving sweep trail
    final sweepPaint = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = borderWidth
      ..shader = SweepGradient(
        startAngle: 0.0,
        endAngle: math.pi * 2,
        transform: GradientRotation(progress * 2 * math.pi),
        colors: [
          Colors.transparent,
          Colors.transparent,
          trailAccentColor.withValues(alpha: 0.35),
          trailColor,
          trailAccentColor.withValues(alpha: 0.8),
          Colors.transparent,
        ],
        stops: const [0.0, 0.7, 0.82, 0.92, 0.96, 1.0],
      ).createShader(rect);

    canvas.drawRRect(rrect, sweepPaint);
  }

  @override
  bool shouldRepaint(covariant _BorderTrailPainter oldDelegate) =>
      oldDelegate.progress != progress ||
      oldDelegate.borderRadius != borderRadius ||
      oldDelegate.borderWidth != borderWidth ||
      oldDelegate.baseBorderColor != baseBorderColor ||
      oldDelegate.trailColor != trailColor ||
      oldDelegate.trailAccentColor != trailAccentColor;
}
