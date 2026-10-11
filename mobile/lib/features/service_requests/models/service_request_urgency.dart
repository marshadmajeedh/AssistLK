enum ServiceRequestUrgency {
  unknown,
  low,
  medium,
  high,
  critical;

  String get displayName {
    switch (this) {
      case ServiceRequestUrgency.unknown:
        return 'Unknown';
      case ServiceRequestUrgency.low:
        return 'Low';
      case ServiceRequestUrgency.medium:
        return 'Medium';
      case ServiceRequestUrgency.high:
        return 'High';
      case ServiceRequestUrgency.critical:
        return 'Critical';
    }
  }

  String toJson() {
    switch (this) {
      case ServiceRequestUrgency.unknown:
        return 'Unknown';
      case ServiceRequestUrgency.low:
        return 'Low';
      case ServiceRequestUrgency.medium:
        return 'Medium';
      case ServiceRequestUrgency.high:
        return 'High';
      case ServiceRequestUrgency.critical:
        return 'Critical';
    }
  }

  static ServiceRequestUrgency fromJson(dynamic json) {
    if (json is ServiceRequestUrgency) {
      return json;
    }

    if (json is int) {
      if (json >= 0 && json < ServiceRequestUrgency.values.length) {
        return ServiceRequestUrgency.values[json];
      }
      return ServiceRequestUrgency.unknown;
    }

    if (json is String) {
      return fromString(json);
    }

    return ServiceRequestUrgency.unknown;
  }

  static ServiceRequestUrgency fromString(String? value) {
    if (value == null || value.trim().isEmpty) {
      return ServiceRequestUrgency.unknown;
    }

    final normalized = value.trim().toLowerCase();
    for (final urgency in ServiceRequestUrgency.values) {
      if (urgency.name.toLowerCase() == normalized ||
          urgency.toJson().toLowerCase() == normalized) {
        return urgency;
      }
    }

    return ServiceRequestUrgency.unknown;
  }
}
