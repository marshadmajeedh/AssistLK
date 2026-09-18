import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../service_requests/models/location_source.dart';
import '../../service_requests/widgets/location_attribution.dart';
import '../../../../shared/theme/app_spacing.dart';
import '../../../../shared/theme/app_text_styles.dart';
import '../../../../shared/widgets/app_card.dart';
import '../providers/customer_location_provider.dart';

class HomeLocationBanner extends StatelessWidget {
  const HomeLocationBanner({super.key});

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<CustomerLocationProvider>();
    final suggestion = provider.suggestion;
    final resolved = provider.state == CustomerLocationState.resolved;
    final message = switch (provider.state) {
      CustomerLocationState.idle =>
        'Use your current location for a faster service request.',
      CustomerLocationState.capturing => 'Finding your location…',
      CustomerLocationState.resolving => 'Finding your address…',
      CustomerLocationState.resolved =>
        provider.freshSuggestion == null
            ? 'Refresh to suggest this location for a new request.'
            : 'Confirm where the service is needed during request creation.',
      CustomerLocationState.denied => 'Location permission is not available. You can enter a service location manually when creating a request.',
      CustomerLocationState.deniedForever => 'Location access is disabled for AssistLK. Enable it in device settings or enter the service location manually.',
      CustomerLocationState.servicesDisabled => 'Location services are turned off. Enable them in device settings or enter the service location manually.',
      CustomerLocationState.captureFailed => 'Could not capture your location. Try again or enter a service location manually.',
      CustomerLocationState.geocodingFailed => 'Could not find an address for your location. Try again or enter a service location manually.',
    };
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            resolved
                ? (provider.freshSuggestion != null
                      ? 'Current location'
                      : 'Last captured location')
                : 'Location not set',
            style: AppTextStyles.cardHeading,
          ),
          const SizedBox(height: AppSpacing.sm),
          if (resolved && suggestion != null) ...[
            Text(
              suggestion.location.formattedAddress,
              style: AppTextStyles.body,
            ),
            if (suggestion.accuracyMeters != null &&
                suggestion.accuracyMeters!.isFinite &&
                suggestion.accuracyMeters! >= 0)
              Text(
                'Accuracy approximately ${suggestion.accuracyMeters!.round()} m',
                style: AppTextStyles.small,
              ),
            if (suggestion.source == LocationSource.openStreetMap)
              const LocationAttribution(),
          ],
          Text(message, style: AppTextStyles.small),
          const SizedBox(height: AppSpacing.sm),
          if (provider.busy)
            const LinearProgressIndicator()
          else
            OutlinedButton.icon(
              onPressed: provider.capture,
              icon: const Icon(Icons.my_location_rounded),
              label: Text(
                resolved
                    ? 'Refresh Location'
                    : provider.state == CustomerLocationState.idle
                    ? 'Use Current Location'
                    : 'Try Again',
              ),
            ),
        ],
      ),
    );
  }
}
