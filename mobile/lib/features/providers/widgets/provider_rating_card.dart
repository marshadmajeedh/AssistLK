import 'package:flutter/material.dart';

import '../../../shared/theme/app_spacing.dart';

class ProviderRatingCard extends StatelessWidget {
  final double averageRating;
  final int totalReviews;

  const ProviderRatingCard({
    super.key,
    required this.averageRating,
    required this.totalReviews,
  });

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(
        AppSpacing.md,
        AppSpacing.xs,
        AppSpacing.md,
        AppSpacing.xs,
      ),
      child: Card(
        elevation: 2,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
        child: Padding(
          padding: const EdgeInsets.symmetric(
            horizontal: AppSpacing.md,
            vertical: AppSpacing.sm,
          ),
          child: Row(
            children: [
              const Icon(Icons.star, color: Colors.amber, size: 30),
              const SizedBox(width: AppSpacing.sm),
              Text(
                averageRating.toStringAsFixed(1),
                style: const TextStyle(
                  fontSize: 24,
                  fontWeight: FontWeight.bold,
                ),
              ),
              const SizedBox(width: AppSpacing.sm),
              Text(
                '($totalReviews reviews)',
                style: TextStyle(color: Colors.grey.shade700, fontSize: 14),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
