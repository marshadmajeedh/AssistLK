/// Status of a quotation, as returned by the backend.
enum QuotationStatus {
  draft,
  sent,
  waitingForCustomerApproval,
  approved,
  rejected,
  expired;

  /// Parse from the backend string (case-insensitive).
  static QuotationStatus fromJson(dynamic raw) {
    if (raw == null) return QuotationStatus.draft;
    final normalized =
        raw.toString().trim().toLowerCase().replaceAll('_', '').replaceAll(' ', '');
    switch (normalized) {
      case 'draft':
        return QuotationStatus.draft;
      case 'sent':
        return QuotationStatus.sent;
      case 'waitingforcustomerapproval':
      case 'pendingapproval':
        return QuotationStatus.waitingForCustomerApproval;
      case 'approved':
        return QuotationStatus.approved;
      case 'rejected':
        return QuotationStatus.rejected;
      case 'expired':
        return QuotationStatus.expired;
      default:
        return QuotationStatus.draft;
    }
  }

  String get displayLabel {
    switch (this) {
      case QuotationStatus.draft:
        return 'Draft';
      case QuotationStatus.sent:
        return 'Sent';
      case QuotationStatus.waitingForCustomerApproval:
        return 'Awaiting Customer';
      case QuotationStatus.approved:
        return 'Approved';
      case QuotationStatus.rejected:
        return 'Rejected';
      case QuotationStatus.expired:
        return 'Expired';
    }
  }

  bool get isDraft => this == QuotationStatus.draft;
  bool get isSent => this == QuotationStatus.sent;
  bool get isPendingApproval =>
      this == QuotationStatus.waitingForCustomerApproval;
  bool get isApproved => this == QuotationStatus.approved;
  bool get isRejected => this == QuotationStatus.rejected;
  bool get isExpired => this == QuotationStatus.expired;
}

/// Booking lifecycle status — mirrors backend BookingStatus integer values.
/// Order MUST match the backend enum.
enum BookingStatus {
  confirmed(0, 'Confirmed'),
  providerAssigned(1, 'Assigned'),
  onTheWay(2, 'On The Way'),
  arrived(3, 'Arrived'),
  inProgress(4, 'In Progress'),
  completed(5, 'Completed'),
  cancelled(6, 'Cancelled');

  final int code;
  final String displayLabel;

  const BookingStatus(this.code, this.displayLabel);

  static BookingStatus fromCode(int code) {
    return BookingStatus.values.firstWhere(
      (s) => s.code == code,
      orElse: () => BookingStatus.confirmed,
    );
  }

  static BookingStatus fromLabel(String? label) {
    if (label == null) return BookingStatus.confirmed;
    final normalized = label.trim().toLowerCase().replaceAll(' ', '');
    switch (normalized) {
      case 'confirmed':
        return BookingStatus.confirmed;
      case 'assigned':
      case 'providerassigned':
        return BookingStatus.providerAssigned;
      case 'ontheway':
        return BookingStatus.onTheWay;
      case 'arrived':
        return BookingStatus.arrived;
      case 'inprogress':
      case 'workstarted':
        return BookingStatus.inProgress;
      case 'completed':
        return BookingStatus.completed;
      case 'cancelled':
      case 'canceled':
        return BookingStatus.cancelled;
      default:
        return BookingStatus.confirmed;
    }
  }

  bool get isTerminal =>
      this == BookingStatus.completed || this == BookingStatus.cancelled;

  bool get isActive =>
      this == BookingStatus.providerAssigned ||
      this == BookingStatus.onTheWay ||
      this == BookingStatus.arrived ||
      this == BookingStatus.inProgress;

  BookingStatus? get next {
    switch (this) {
      case BookingStatus.confirmed:
        return BookingStatus.providerAssigned;
      case BookingStatus.providerAssigned:
        return BookingStatus.onTheWay;
      case BookingStatus.onTheWay:
        return BookingStatus.arrived;
      case BookingStatus.arrived:
        return BookingStatus.inProgress;
      case BookingStatus.inProgress:
        return BookingStatus.completed;
      default:
        return null;
    }
  }
}