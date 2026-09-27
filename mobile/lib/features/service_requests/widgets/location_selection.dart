import 'package:flutter/material.dart';

import '../../../../shared/theme/app_colors.dart';
import '../../../../shared/theme/app_radius.dart';
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
          if (c.candidates.isNotEmpty) ...[
            AppCard(
              key: const Key('forward_candidates_card'),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    c.candidates.length == 1
                        ? 'Resolved service location'
                        : 'Matching locations (${c.candidates.length})',
                    style: AppTextStyles.cardHeading,
                  ),
                  const SizedBox(height: AppSpacing.xs),
                  Text(
                    c.candidates.length == 1
                        ? 'Confirm this location to set your service coordinates:'
                        : 'Select the address that matches your service location:',
                    style: AppTextStyles.small,
                  ),
                  const SizedBox(height: AppSpacing.sm),
                  for (final candidate in c.candidates) ...[
                    InkWell(
                      key: Key('candidate_tile_${candidate.placeId ?? candidate.displayAddress.hashCode}'),
                      onTap: () => c.selectCandidate(candidate),
                      borderRadius: BorderRadius.circular(AppRadius.medium),
                      child: Container(
                        padding: const EdgeInsets.symmetric(
                          vertical: AppSpacing.xs,
                          horizontal: AppSpacing.xs,
                        ),
                        decoration: BoxDecoration(
                          color: c.selectedCandidate == candidate
                              ? AppColors.primarySurface
                              : Colors.transparent,
                          borderRadius: BorderRadius.circular(AppRadius.medium),
                          border: Border.all(
                            color: c.selectedCandidate == candidate
                                ? AppColors.primary
                                : AppColors.border,
                          ),
                        ),
                        child: Row(
                          children: [
                            Icon(
                              c.selectedCandidate == candidate
                                  ? Icons.radio_button_checked
                                  : Icons.radio_button_unchecked,
                              color: c.selectedCandidate == candidate
                                  ? AppColors.primary
                                  : AppColors.textSecondary,
                              size: 20,
                            ),
                            const SizedBox(width: AppSpacing.sm),
                            Expanded(
                              child: Text(
                                candidate.displayAddress,
                                style: AppTextStyles.body,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                    const SizedBox(height: AppSpacing.xs),
                  ],
                  const SizedBox(height: AppSpacing.sm),
                  const LocationAttribution(),
                  const SizedBox(height: AppSpacing.md),
                  AppButton(
                    key: const Key('use_forward_location_button'),
                    text: 'Use This Location',
                    onPressed: c.selectedCandidate != null &&
                            c.selectedCandidate!.displayAddress.length <= 255
                        ? () => c.confirmCandidate()
                        : null,
                  ),
                  const SizedBox(height: AppSpacing.sm),
                  SizedBox(
                    width: double.infinity,
                    child: OutlinedButton(
                      key: const Key('cancel_forward_candidates_button'),
                      onPressed: c.dismissCandidates,
                      child: const Text('Cancel / Keep Editing'),
                    ),
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
          if (!c.hasConfirmedCoordinates &&
              c.text.text.trim().isNotEmpty &&
              c.candidates.isEmpty &&
              c.preview == null &&
              !c.busy) ...[
            const SizedBox(height: AppSpacing.xs),
            Align(
              alignment: Alignment.centerRight,
              child: TextButton.icon(
                key: const Key('resolve_address_button'),
                onPressed: c.resolveAddress,
                icon: const Icon(Icons.travel_explore_rounded, size: 18),
                label: const Text('Resolve Address'),
              ),
            ),
          ],
          if (c.hasConfirmedCoordinates) ...[
            const SizedBox(height: AppSpacing.xs),
            Row(
              children: [
                const Icon(
                  Icons.check_circle_rounded,
                  size: 16,
                  color: AppColors.success,
                ),
                const SizedBox(width: AppSpacing.xs),
                Expanded(
                  child: Text(
                    c.source == LocationSource.openStreetMap && !c.fromGps
                        ? 'Address resolved & coordinates confirmed'
                        : 'GPS location captured',
                    style: AppTextStyles.small.copyWith(
                      color: AppColors.success,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ),
              ],
            ),
          ],
          if (c.source == LocationSource.openStreetMap)
            const LocationAttribution(),
          if (c.hasGps && c.fromGps) ...[
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
    final label = editing || (c.hasGps && c.fromGps)
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
