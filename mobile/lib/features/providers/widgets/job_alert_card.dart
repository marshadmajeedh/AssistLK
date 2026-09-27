import 'package:flutter/material.dart';

import '../../../shared/theme/app_spacing.dart';
import '../../../shared/theme/app_text_styles.dart';

class JobAlertCard extends StatelessWidget {
  final String category;
  final String distance;
  final String urgency;
  final String? description;
  final String? aiRationale;
  final bool isOutOfRange;
  final VoidCallback? onAccept;
  final VoidCallback? onDecline;

  const JobAlertCard({
    super.key,
    required this.category,
    required this.distance,
    required this.urgency,
    this.description,
    this.aiRationale,
    this.isOutOfRange = false,
    this.onAccept,
    this.onDecline,
  });

  @override
  Widget build(BuildContext context) {
    return Card(
      elevation: 6,
      margin: EdgeInsets.zero,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(14),
        side: isOutOfRange
            ? const BorderSide(color: Colors.amber, width: 2)
            : BorderSide.none,
      ),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Row(
                  children: [
                    Text(
                      category,
                      style: AppTextStyles.sectionHeading.copyWith(fontSize: 17),
                    ),
                    const SizedBox(width: 8),
                    Container(
                      padding: const EdgeInsets.symmetric(
                          horizontal: 8, vertical: 2),
                      decoration: BoxDecoration(
                        color: urgency.toLowerCase() == 'high'
                            ? Colors.red.shade100
                            : Colors.orange.shade100,
                        borderRadius: BorderRadius.circular(6),
                      ),
                      child: Text(
                        urgency,
                        style: TextStyle(
                          color: urgency.toLowerCase() == 'high'
                              ? Colors.red.shade900
                              : Colors.orange.shade900,
                          fontWeight: FontWeight.bold,
                          fontSize: 11,
                        ),
                      ),
                    ),
                  ],
                ),
                Row(
                  children: [
                    Icon(
                      Icons.location_on,
                      size: 15,
                      color: isOutOfRange
                          ? Colors.amber.shade900
                          : Colors.blueGrey,
                    ),
                    const SizedBox(width: 3),
                    Text(
                      isOutOfRange
                          ? 'Out of radius ($distance)'
                          : '$distance away',
                      style: TextStyle(
                        color: isOutOfRange
                            ? Colors.amber.shade900
                            : Colors.grey.shade700,
                        fontSize: 12,
                        fontWeight: isOutOfRange
                            ? FontWeight.bold
                            : FontWeight.w500,
                      ),
                    ),
                  ],
                ),
              ],
            ),
            if (description != null && description!.isNotEmpty) ...[
              const SizedBox(height: 6),
              Text(
                'Customer: "$description"',
                style: TextStyle(
                  fontSize: 12,
                  fontStyle: FontStyle.italic,
                  color: Colors.grey.shade800,
                ),
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
              ),
            ],
            if (aiRationale != null && aiRationale!.isNotEmpty) ...[
              const SizedBox(height: 8),
              Container(
                width: double.infinity,
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
                decoration: BoxDecoration(
                  color: Colors.blue.shade50.withValues(alpha: 0.9),
                  borderRadius: BorderRadius.circular(8),
                  border: Border.all(color: Colors.blue.shade200),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Icon(Icons.auto_awesome,
                            size: 14, color: Colors.blue.shade700),
                        const SizedBox(width: 5),
                        Text(
                          'AI Problem Review',
                          style: TextStyle(
                            fontSize: 11,
                            fontWeight: FontWeight.bold,
                            color: Colors.blue.shade900,
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 4),
                    Text(
                      aiRationale!,
                      style: TextStyle(
                        fontSize: 12,
                        height: 1.3,
                        color: Colors.blue.shade900,
                      ),
                      maxLines: 3,
                      overflow: TextOverflow.ellipsis,
                    ),
                  ],
                ),
              ),
            ],
            const SizedBox(height: 10),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed: onDecline ?? () {},
                    style: OutlinedButton.styleFrom(
                      padding: const EdgeInsets.symmetric(vertical: 12),
                      foregroundColor: Colors.red,
                      side: const BorderSide(color: Colors.red),
                    ),
                    child: const Text('Decline'),
                  ),
                ),
                const SizedBox(width: AppSpacing.md),
                Expanded(
                  child: ElevatedButton(
                    onPressed: isOutOfRange ? null : (onAccept ?? () {}),
                    style: ElevatedButton.styleFrom(
                      padding: const EdgeInsets.symmetric(vertical: 12),
                      backgroundColor: isOutOfRange ? Colors.grey : Theme.of(context).primaryColor,
                      foregroundColor: Colors.white,
                    ),
                    child: const Text('Accept Match'),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
