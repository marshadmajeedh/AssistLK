import '../../../../shared/theme/app_assets.dart';

/// Presentation only: use the authoritative category, never CategoryHint.
String? serviceRequestCategoryAsset(String category) => switch (category.trim()) {
  'Unclassified' || '' => AppAssets.unclassifiedService,
  'Plumbing' => AppAssets.plumbingService,
  'Electrical' => AppAssets.electricalService,
  'Vehicle Repair' || 'Vehicle Assistance' => AppAssets.vehicleService,
  'Appliance Repair' => AppAssets.applianceService,
  _ => null,
};
