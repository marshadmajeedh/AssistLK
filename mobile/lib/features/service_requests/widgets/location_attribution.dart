import 'package:flutter/material.dart';
import 'package:url_launcher/link.dart';
import '../../../../shared/theme/app_text_styles.dart';
import '../../../../shared/theme/app_colors.dart';

/// One accessible link shared by every displayed OSM-derived address.
class LocationAttribution extends StatelessWidget {
  const LocationAttribution({super.key});

  @override
  Widget build(BuildContext context) => Link(
    uri: Uri.parse('https://www.openstreetmap.org/copyright'),
    target: LinkTarget.blank,
    builder: (context, followLink) => TextButton(
      onPressed: followLink,
      style: TextButton.styleFrom(
        minimumSize: const Size(48, 48),
        padding: EdgeInsets.zero,
        alignment: Alignment.centerLeft,
        foregroundColor: AppColors.primary,
        textStyle: AppTextStyles.small.copyWith(decoration: TextDecoration.underline),
      ),
      child: const Text('\u00a9 OpenStreetMap contributors'),
    ),
  );
}
