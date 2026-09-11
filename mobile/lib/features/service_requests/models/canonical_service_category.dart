import 'package:flutter/material.dart';

class CanonicalServiceCategory {
  final String canonicalName;
  final String displayName;
  final String description;
  final IconData icon;

  const CanonicalServiceCategory({
    required this.canonicalName,
    required this.displayName,
    required this.description,
    required this.icon,
  });

  static const CanonicalServiceCategory plumbing = CanonicalServiceCategory(
    canonicalName: 'Plumbing',
    displayName: 'Plumbing',
    description: 'Pipes, leaks, taps, drains',
    icon: Icons.plumbing_rounded,
  );

  static const CanonicalServiceCategory electrical = CanonicalServiceCategory(
    canonicalName: 'Electrical',
    displayName: 'Electrical',
    description: 'Wiring, circuit breakers, power',
    icon: Icons.bolt_rounded,
  );

  static const CanonicalServiceCategory vehicleRepair = CanonicalServiceCategory(
    canonicalName: 'Vehicle Repair',
    displayName: 'Vehicle Assistance',
    description: 'Stalled vehicle, battery, puncture',
    icon: Icons.directions_car_rounded,
  );

  static const CanonicalServiceCategory applianceRepair = CanonicalServiceCategory(
    canonicalName: 'Appliance Repair',
    displayName: 'Appliance Repair',
    description: 'Fridge, washing machine, oven',
    icon: Icons.home_repair_service_rounded,
  );

  static const List<CanonicalServiceCategory> canonicalShortcuts = [
    plumbing,
    electrical,
    vehicleRepair,
    applianceRepair,
  ];

  static CanonicalServiceCategory? fromCanonicalOrDisplayName(String? name) {
    if (name == null || name.trim().isEmpty) return null;
    final normalized = name.trim().toLowerCase();
    for (final cat in canonicalShortcuts) {
      if (cat.canonicalName.toLowerCase() == normalized ||
          cat.displayName.toLowerCase() == normalized) {
        return cat;
      }
    }
    return null;
  }

  /// Returns the canonical CategoryHint string for transport, or null if
  /// 'Let AI identify', null, or empty.
  ///
  /// Mappings:
  /// - Plumbing -> Plumbing
  /// - Electrical -> Electrical
  /// - Vehicle Assistance -> Vehicle Repair
  /// - Vehicle Repair -> Vehicle Repair
  /// - Appliance Repair -> Appliance Repair
  /// - Let AI identify -> null
  /// - null / empty -> null
  ///
  /// Throws [ArgumentError] for unsupported or invalid values (e.g. "Unclassified", "Cleaning", "AC Service").
  static String? toCanonicalCategoryHint(String? input) {
    if (input == null) return null;
    final trimmed = input.trim();
    if (trimmed.isEmpty ||
        trimmed.toLowerCase() == 'let ai identify' ||
        trimmed.toLowerCase() == 'let assistlk ai identify') {
      return null;
    }
    final category = fromCanonicalOrDisplayName(trimmed);
    if (category != null) {
      return category.canonicalName;
    }
    throw ArgumentError.value(
      input,
      'input',
      'Unsupported category preference: "$input". Expected one of: Plumbing, Electrical, Vehicle Assistance, Appliance Repair, or "Let AI identify".',
    );
  }
}
