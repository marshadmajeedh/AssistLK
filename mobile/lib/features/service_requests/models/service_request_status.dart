enum ServiceRequestStatus {
  created,
  analyzing,
  awaitingInformation,
  analyzed,
  readyForMatching,
  cancelled;

  String get displayName {
    switch (this) {
      case ServiceRequestStatus.created:
        return 'Created';
      case ServiceRequestStatus.analyzing:
        return 'Analyzing';
      case ServiceRequestStatus.awaitingInformation:
        return 'Awaiting Information';
      case ServiceRequestStatus.analyzed:
        return 'Analyzed';
      case ServiceRequestStatus.readyForMatching:
        return 'Ready For Matching';
      case ServiceRequestStatus.cancelled:
        return 'Cancelled';
    }
  }

  String toJson() {
    switch (this) {
      case ServiceRequestStatus.created:
        return 'Created';
      case ServiceRequestStatus.analyzing:
        return 'Analyzing';
      case ServiceRequestStatus.awaitingInformation:
        return 'AwaitingInformation';
      case ServiceRequestStatus.analyzed:
        return 'Analyzed';
      case ServiceRequestStatus.readyForMatching:
        return 'ReadyForMatching';
      case ServiceRequestStatus.cancelled:
        return 'Cancelled';
    }
  }

  static ServiceRequestStatus fromJson(dynamic json) {
    if (json is ServiceRequestStatus) {
      return json;
    }

    if (json is int) {
      if (json >= 0 && json < ServiceRequestStatus.values.length) {
        return ServiceRequestStatus.values[json];
      }
      return ServiceRequestStatus.created;
    }

    if (json is String) {
      return fromString(json);
    }

    return ServiceRequestStatus.created;
  }

  static ServiceRequestStatus fromString(String? value) {
    if (value == null || value.trim().isEmpty) {
      return ServiceRequestStatus.created;
    }

    final normalized = value.trim().toLowerCase();
    for (final status in ServiceRequestStatus.values) {
      if (status.name.toLowerCase() == normalized ||
          status.toJson().toLowerCase() == normalized ||
          status.displayName.toLowerCase() == normalized) {
        return status;
      }
    }

    return ServiceRequestStatus.created;
  }
}
