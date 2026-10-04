/// Quotation lifecycle states, matching the .NET backend enum.
enum QuotationStatus {
  draft('Draft'),
  sent('Sent'),
  waitingForCustomerApproval('WaitingForCustomerApproval'),
  approved('Approved'),
  rejected('Rejected'),
  expired('Expired');

  const QuotationStatus(this.wireValue);

  /// The exact string used by the .NET API and stored in the database.
  final String wireValue;

  static QuotationStatus fromJson(Object? value) {
    if (value is String) {
      final normalized = value.trim().toLowerCase();
      for (final s in QuotationStatus.values) {
        if (s.wireValue.toLowerCase() == normalized) return s;
      }
    }
    return QuotationStatus.draft;
  }

  String toJson() => wireValue;

  bool get isDraft => this == QuotationStatus.draft;
  bool get isPendingApproval =>
      this == QuotationStatus.waitingForCustomerApproval;
  bool get isApproved => this == QuotationStatus.approved;
  bool get isRejected => this == QuotationStatus.rejected;
  bool get isExpired => this == QuotationStatus.expired;

  /// Customer-friendly label for UI display.
  String get displayLabel {
    switch (this) {
      case QuotationStatus.draft:
        return 'Draft';
      case QuotationStatus.sent:
        return 'Sent';
      case QuotationStatus.waitingForCustomerApproval:
        return 'Awaiting your approval';
      case QuotationStatus.approved:
        return 'Approved';
      case QuotationStatus.rejected:
        return 'Rejected';
      case QuotationStatus.expired:
        return 'Expired';
    }
  }
}