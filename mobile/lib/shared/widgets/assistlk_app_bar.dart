import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../theme/app_colors.dart';

/// Reusable canonical AssistLK navy top app bar component.
///
/// Ensures visual consistency across request-flow states and screens:
/// - Background: Canonical AssistLK navy (`AppColors.primary`)
/// - Foreground: White
/// - Back button: White
/// - Status bar icons: Light overlay appearance
/// - Flat elevation: 0
class AssistLKAppBar extends StatelessWidget implements PreferredSizeWidget {
  final Widget? title;
  final Widget? leading;
  final List<Widget>? actions;
  final PreferredSizeWidget? bottom;
  final bool automaticallyImplyLeading;
  final Color? backgroundColor;
  final Color? foregroundColor;
  final double? elevation;
  final SystemUiOverlayStyle? systemOverlayStyle;
  final IconThemeData? iconTheme;
  final IconThemeData? actionsIconTheme;

  const AssistLKAppBar({
    super.key,
    this.title,
    this.leading,
    this.actions,
    this.bottom,
    this.automaticallyImplyLeading = true,
    this.backgroundColor,
    this.foregroundColor,
    this.elevation,
    this.systemOverlayStyle,
    this.iconTheme,
    this.actionsIconTheme,
  });

  @override
  Widget build(BuildContext context) {
    return AppBar(
      title: title,
      leading: leading,
      actions: actions,
      bottom: bottom,
      automaticallyImplyLeading: automaticallyImplyLeading,
      backgroundColor: backgroundColor ?? AppColors.primary,
      foregroundColor: foregroundColor ?? Colors.white,
      elevation: elevation ?? 0,
      systemOverlayStyle: systemOverlayStyle ?? SystemUiOverlayStyle.light,
      iconTheme: iconTheme ?? const IconThemeData(color: Colors.white),
      actionsIconTheme:
          actionsIconTheme ?? const IconThemeData(color: Colors.white),
    );
  }

  @override
  Size get preferredSize =>
      Size.fromHeight(kToolbarHeight + (bottom?.preferredSize.height ?? 0.0));
}
