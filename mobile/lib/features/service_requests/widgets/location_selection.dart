import 'package:flutter/material.dart';

import '../../../../shared/theme/app_spacing.dart';
import '../../../../shared/theme/app_text_styles.dart';
import '../../../../shared/widgets/app_card.dart';
import '../../../../shared/widgets/app_text_field.dart';
import '../models/location_source.dart';
import '../providers/location_selection_controller.dart';
import 'location_attribution.dart';

class LocationSelection extends StatelessWidget {
  final LocationSelectionController controller;
  final bool editing;
  const LocationSelection({
    super.key,
    required this.controller,
    this.editing = false,
  });

  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: controller,
    builder: (context, _) {
      final c = controller;
      return Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('Service Location', style: AppTextStyles.sectionHeading),
          const SizedBox(height: AppSpacing.sm),
          Wrap(
            spacing: AppSpacing.sm,
            runSpacing: AppSpacing.xs,
            children: [
              OutlinedButton.icon(
                key: Key(
                  editing
                      ? 'edit_update_gps_button'
                      : 'use_current_location_button',
                ),
                onPressed: c.capture,
                icon: const Icon(Icons.my_location_rounded),
                label: Text(
                  editing
                      ? 'Refresh Current Location'
                      : c.hasGps
                      ? 'Refresh'
                      : 'Use Current Location',
                ),
              ),
              TextButton(
                onPressed: c.enterManually,
                child: const Text('Enter location manually'),
              ),
            ],
          ),
          if (c.busy) ...[
            const LinearProgressIndicator(),
            const Text(
              'Finding your service location…',
              style: AppTextStyles.small,
            ),
          ],
          if (c.preview != null) ...[
            AppCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Current service location',
                    style: AppTextStyles.cardHeading,
                  ),
                  Text(c.preview!.formattedAddress, style: AppTextStyles.body),
                  const LocationAttribution(),
                  const SizedBox(height: AppSpacing.sm),
                  const Text('Location captured', style: AppTextStyles.small),
                  if (c.accuracy != null &&
                      c.accuracy!.isFinite &&
                      c.accuracy! >= 0)
                    Text(
                      'Accuracy approximately ${c.accuracy!.round()} m',
                      style: AppTextStyles.small,
                    ),
                  FilledButton(
                    onPressed: c.preview!.formattedAddress.length <= 255
                        ? c.confirm
                        : null,
                    child: const Text('Use This Location'),
                  ),
                ],
              ),
            ),
          ],
          if (c.message != null)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: AppSpacing.sm),
              child: Text(c.message!, style: AppTextStyles.body),
            ),
          const SizedBox(height: AppSpacing.sm),
          AppTextField(
            controller: c.text,
            label: 'Location / Address',
            hint: 'Enter the service address or a nearby landmark',
            validator: (_) => c.validate(),
          ),
          if (c.source == LocationSource.openStreetMap)
            const LocationAttribution(),
          if (c.hasGps) ...[
            const Text('GPS location captured', style: AppTextStyles.small),
            if (c.needsGpsChoice && c.preview == null && !c.busy) ...[
              const Text(
                'Address changed with attached GPS',
                key: Key('edit_gps_confirmation_prompt'),
                style: AppTextStyles.cardHeading,
              ),
              const Text(
                'Confirm whether the captured GPS belongs to this address.',
              ),
              OutlinedButton(
                key: const Key('edit_keep_gps_button'),
                onPressed: c.keepGps,
                child: const Text('Keep captured GPS for this edited address'),
              ),
            ],
            TextButton(
              key: const Key('edit_remove_gps_button'),
              onPressed: c.removeGps,
              child: const Text('Remove captured GPS'),
            ),
          ],
        ],
      );
    },
  );
}
