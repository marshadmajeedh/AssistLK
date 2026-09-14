import 'package:flutter/material.dart';

import '../../../../shared/theme/app_spacing.dart';
import '../../../../shared/theme/app_text_styles.dart';
import '../../../../shared/widgets/app_card.dart';
import '../../../../shared/widgets/app_button.dart';
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
          _buildActions(context, c),
          const SizedBox(height: AppSpacing.sm),
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
                  Text(
                    c.isSuggested
                        ? 'Suggested service location'
                        : 'Current service location',
                    style: AppTextStyles.cardHeading,
                  ),
                  const SizedBox(height: AppSpacing.sm),
                  Text(c.preview!.formattedAddress, style: AppTextStyles.body),
                  const SizedBox(height: AppSpacing.sm),
                  if (c.accuracy != null &&
                      c.accuracy!.isFinite &&
                      c.accuracy! >= 0)
                    Text(
                      'Accuracy approximately ${c.accuracy!.round()} m',
                      style: AppTextStyles.small,
                    ),
                  if (c.previewSource == LocationSource.openStreetMap) ...[
                    const SizedBox(height: AppSpacing.sm + AppSpacing.xs),
                    const LocationAttribution(),
                  ],
                  const SizedBox(height: AppSpacing.md),
                  LayoutBuilder(
                    builder: (context, constraints) {
                      final label = TextPainter(
                        text: const TextSpan(
                          text: 'Use This Location',
                          style: AppTextStyles.button,
                        ),
                        textDirection: Directionality.of(context),
                        textScaler: MediaQuery.textScalerOf(context),
                      )..layout();
                      final fits =
                          label.width + 2 * AppSpacing.lg <=
                          constraints.maxWidth;
                      label.dispose();
                      return AppButton(
                        text: 'Use This Location',
                        maxLines: fits ? 1 : null,
                        onPressed: c.preview!.formattedAddress.length <= 255
                            ? c.confirm
                            : null,
                      );
                    },
                  ),
                  if (c.isSuggested) ...[
                    const SizedBox(height: AppSpacing.md),
                    SizedBox(
                      width: double.infinity,
                      child: OutlinedButton(
                        onPressed: c.enterManually,
                        child: const Text('Change Location'),
                      ),
                    ),
                  ],
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
            const SizedBox(height: AppSpacing.sm + AppSpacing.xs),
            if (c.preview == null)
              const Text('GPS location captured', style: AppTextStyles.small),
            if (c.needsGpsChoice && c.preview == null && !c.busy) ...[
              const SizedBox(height: AppSpacing.md),
              const Text(
                'Address changed with attached GPS',
                key: Key('edit_gps_confirmation_prompt'),
                style: AppTextStyles.cardHeading,
              ),
              const SizedBox(height: AppSpacing.sm),
              const Text(
                'Confirm whether the captured GPS belongs to this address.',
              ),
              const SizedBox(height: AppSpacing.md),
              OutlinedButton(
                key: const Key('edit_keep_gps_button'),
                onPressed: c.keepGps,
                child: const Text('Keep captured GPS for this edited address'),
              ),
            ],
            const SizedBox(height: AppSpacing.sm + AppSpacing.xs),
            SizedBox(
              width: double.infinity,
              child: TextButton(
                key: const Key('edit_remove_gps_button'),
                style: TextButton.styleFrom(
                  alignment: Alignment.centerLeft,
                  padding: EdgeInsets.zero,
                  minimumSize: const Size(48, 48),
                ),
                onPressed: c.removeGps,
                child: const Text('Remove captured GPS'),
              ),
            ),
          ],
        ],
      );
    },
  );
  Widget _buildActions(BuildContext context, LocationSelectionController c) {
    final label = editing || c.hasGps
        ? 'Refresh Location'
        : 'Use Current Location';
    double textWidth(String value) {
      final painter = TextPainter(
        text: TextSpan(text: value, style: AppTextStyles.button),
        textDirection: Directionality.of(context),
        textScaler: MediaQuery.textScalerOf(context),
      )..layout();
      final width = painter.width;
      painter.dispose();
      return width;
    }

    // Reserve the themed padding and icon space; respect accessibility text size.
    final refreshWidth = textWidth(label) + 2 * AppSpacing.md + 32;
    final manualWidth = textWidth('Enter Manually') + 2 * AppSpacing.md;
    final minimumButtonWidth = refreshWidth > manualWidth
        ? refreshWidth
        : manualWidth;
    return LayoutBuilder(
      builder: (context, constraints) {
        final refresh = OutlinedButton.icon(
          key: Key(
            editing ? 'edit_update_gps_button' : 'use_current_location_button',
          ),
          onPressed: c.capture,
          icon: const Icon(Icons.my_location_rounded),
          label: Text(label, textAlign: TextAlign.center),
        );
        final manual = OutlinedButton(
          onPressed: c.enterManually,
          child: const Text('Enter Manually', textAlign: TextAlign.center),
        );
        if (constraints.maxWidth < minimumButtonWidth * 2 + AppSpacing.sm) {
          return Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              refresh,
              const SizedBox(height: AppSpacing.sm),
              manual,
            ],
          );
        }
        return Row(
          children: [
            Expanded(child: refresh),
            const SizedBox(width: AppSpacing.sm),
            Expanded(child: manual),
          ],
        );
      },
    );
  }
}
