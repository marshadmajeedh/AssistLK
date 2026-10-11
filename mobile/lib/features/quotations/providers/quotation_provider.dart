import 'package:flutter/foundation.dart';

import '../models/booking_model.dart';
import '../models/quotation_item_model.dart';
import '../models/quotation_model.dart';
import '../models/quotation_status.dart';
import '../services/quotation_service.dart';

class QuotationProvider extends ChangeNotifier {
  final QuotationService quotationService;

  QuotationProvider({required this.quotationService});

  // ── Customer-side state ────────────────────────────────────────
  List<QuotationModel> _quotations = [];
  QuotationModel? _currentQuotation;
  BookingModel? _lastBooking;
  bool _isLoading = false;
  bool _isSubmitting = false;
  String? _error;

  // ── Provider-side state ────────────────────────────────────────
  List<ProviderBookingSummaryModel> _providerBookings = [];
  List<ProviderBookingSummaryModel> _providerCompletedBookings = [];
  bool _isProviderBookingsLoading = false;
  String? _providerBookingsError;
  QuotationModel? _pendingProviderDraft;

  bool _isDisposed = false;
  int _generation = 0;

  bool _isCurrent(int generation) => !_isDisposed && generation == _generation;

  @override
  void dispose() {
    _generation++;
    _isDisposed = true;
    super.dispose();
  }

  @override
  void notifyListeners() {
    if (!_isDisposed) super.notifyListeners();
  }

  // ── Customer-side getters ──────────────────────────────────────
  List<QuotationModel> get quotations => List.unmodifiable(_quotations);
  QuotationModel? get currentQuotation => _currentQuotation;
  BookingModel? get lastBooking => _lastBooking;
  bool get isLoading => _isLoading;
  bool get isSubmitting => _isSubmitting;
  String? get error => _error;

  // ── Provider-side getters ──────────────────────────────────────
  List<ProviderBookingSummaryModel> get providerBookings =>
      List.unmodifiable(_providerBookings);
  List<ProviderBookingSummaryModel> get providerCompletedBookings =>
      List.unmodifiable(_providerCompletedBookings);
  bool get isProviderBookingsLoading => _isProviderBookingsLoading;
  String? get providerBookingsError => _providerBookingsError;
  bool get hasUnsentProviderDraft => _pendingProviderDraft != null;

  void clearError() {
    _error = null;
    notifyListeners();
  }

  void setCurrentQuotation(QuotationModel? quotation) {
    _currentQuotation = quotation;
    notifyListeners();
  }

  void reset() {
    _generation++;
    _quotations = [];
    _currentQuotation = null;
    _lastBooking = null;
    _isLoading = false;
    _isSubmitting = false;
    _error = null;
    _providerBookings = [];
    _providerCompletedBookings = [];
    _isProviderBookingsLoading = false;
    _providerBookingsError = null;
    _pendingProviderDraft = null;
    notifyListeners();
  }

  // ─────────────────────────────────────────────────────────────
  // CUSTOMER METHODS
  // ─────────────────────────────────────────────────────────────

  Future<bool> loadForServiceRequest(String serviceRequestId) async {
    final generation = _generation;
    if (!_isCurrent(generation)) return false;
    _isLoading = true;
    _error = null;
    notifyListeners();

    try {
      final fetched = await quotationService.listForServiceRequest(
        serviceRequestId,
      );
      if (!_isCurrent(generation)) return false;

      _quotations = List<QuotationModel>.from(fetched)
        ..sort((a, b) => b.createdAt.compareTo(a.createdAt));

      if (_currentQuotation != null &&
          !_quotations.any((q) => q.id == _currentQuotation!.id)) {
        _currentQuotation = null;
      }
      return true;
    } catch (err) {
      if (!_isCurrent(generation)) return false;
      _error = quotationService.getErrorMessage(err);
      return false;
    } finally {
      if (_isCurrent(generation)) {
        _isLoading = false;
        notifyListeners();
      }
    }
  }

  Future<bool> loadForCurrentCustomer() async {
    final generation = _generation;
    if (!_isCurrent(generation)) return false;
    _isLoading = true;
    _error = null;
    notifyListeners();

    try {
      final fetched = await quotationService.listForCurrentCustomer();
      if (!_isCurrent(generation)) return false;

      _quotations = List<QuotationModel>.from(fetched)
        ..sort((a, b) => b.createdAt.compareTo(a.createdAt));
      return true;
    } catch (err) {
      if (!_isCurrent(generation)) return false;
      _error = quotationService.getErrorMessage(err);
      return false;
    } finally {
      if (_isCurrent(generation)) {
        _isLoading = false;
        notifyListeners();
      }
    }
  }

  Future<QuotationModel?> loadById(int quotationId) async {
    final generation = _generation;
    if (!_isCurrent(generation)) return null;
    _isLoading = true;
    _error = null;
    notifyListeners();

    try {
      final quotation = await quotationService.getById(quotationId);
      if (!_isCurrent(generation)) return null;
      _currentQuotation = quotation;
      _updateInList(quotation);
      return quotation;
    } catch (err) {
      if (!_isCurrent(generation)) return null;
      _error = quotationService.getErrorMessage(err);
      return null;
    } finally {
      if (_isCurrent(generation)) {
        _isLoading = false;
        notifyListeners();
      }
    }
  }

  Future<BookingModel?> approve({
    required int quotationId,
    required String threadId,
    String? customerRemarks,
  }) async {
    final generation = _generation;
    if (!_isCurrent(generation)) return null;
    _isSubmitting = true;
    _error = null;
    notifyListeners();

    try {
      final booking = await quotationService.approve(
        quotationId,
        threadId: threadId,
        customerRemarks: customerRemarks,
      );
      if (!_isCurrent(generation)) return null;
      _lastBooking = booking;

      final matchIndex = _quotations.indexWhere((q) => q.id == quotationId);
      if (matchIndex >= 0) {
        _quotations[matchIndex] = _quotations[matchIndex].copyWith(
          status: QuotationStatus.approved,
        );
      }
      if (_currentQuotation?.id == quotationId) {
        _currentQuotation = _currentQuotation!.copyWith(
          status: QuotationStatus.approved,
        );
      }
      return booking;
    } catch (err) {
      if (!_isCurrent(generation)) return null;
      _error = quotationService.getErrorMessage(err);
      return null;
    } finally {
      if (_isCurrent(generation)) {
        _isSubmitting = false;
        notifyListeners();
      }
    }
  }

  Future<QuotationModel?> reject({
    required int quotationId,
    required String threadId,
    required String reason,
  }) async {
    final generation = _generation;
    if (!_isCurrent(generation)) return null;
    _isSubmitting = true;
    _error = null;
    notifyListeners();

    try {
      final updated = await quotationService.reject(
        quotationId,
        threadId: threadId,
        reason: reason,
      );
      if (!_isCurrent(generation)) return null;
      _updateInList(updated);
      return updated;
    } catch (err) {
      if (!_isCurrent(generation)) return null;
      _error = quotationService.getErrorMessage(err);
      return null;
    } finally {
      if (_isCurrent(generation)) {
        _isSubmitting = false;
        notifyListeners();
      }
    }
  }

  // ─────────────────────────────────────────────────────────────
  // PROVIDER METHODS
  // ─────────────────────────────────────────────────────────────

  /// Loads both upcoming and completed provider bookings in parallel.
  Future<bool> loadProviderBookings() async {
    final generation = _generation;
    if (!_isCurrent(generation)) return false;
    _isProviderBookingsLoading = true;
    _providerBookingsError = null;
    notifyListeners();

    try {
      final results = await Future.wait([
        quotationService.getProviderBookings(completed: false),
        quotationService.getProviderBookings(completed: true),
      ]);
      if (!_isCurrent(generation)) return false;

      _providerBookings = results[0];
      _providerCompletedBookings = results[1];
      return true;
    } catch (err) {
      if (!_isCurrent(generation)) return false;
      _providerBookingsError = quotationService.getErrorMessage(err);
      return false;
    } finally {
      if (_isCurrent(generation)) {
        _isProviderBookingsLoading = false;
        notifyListeners();
      }
    }
  }

  /// Advances a booking's status.
  Future<bool> updateProviderBookingStatus({
    required int bookingId,
    required BookingStatus newStatus,
    String? reason,
  }) async {
    final generation = _generation;
    if (!_isCurrent(generation)) return false;
    _isSubmitting = true;
    _providerBookingsError = null;
    notifyListeners();

    try {
      await quotationService.updateProviderBookingStatus(
        bookingId,
        newStatus: newStatus,
        reason: reason,
      );
      if (!_isCurrent(generation)) return false;

      _replaceBookingLocally(bookingId, newStatus);
      return true;
    } catch (err) {
      if (!_isCurrent(generation)) return false;
      _providerBookingsError = quotationService.getErrorMessage(err);
      return false;
    } finally {
      if (_isCurrent(generation)) {
        _isSubmitting = false;
        notifyListeners();
      }
    }
  }

  /// Creates a quotation and immediately sends it to the customer.
  Future<QuotationModel?> createAndSendQuotation({
    required String serviceRequestId,
    required String providerId,
    required List<QuotationItemModel> items,
    String? notes,
  }) async {
    if (_pendingProviderDraft != null) {
      _error =
          'A saved quotation is waiting to be sent. Retry sending it first.';
      notifyListeners();
      return null;
    }

    return _runProviderQuotationSubmission(() async {
      final draft = await quotationService.createProviderQuotation(
        serviceRequestId: serviceRequestId,
        providerId: providerId,
        items: items,
        notes: notes,
      );
      _pendingProviderDraft = draft;
      notifyListeners();
      return quotationService.sendProviderQuotation(draft.id);
    });
  }

  Future<QuotationModel?> retrySendProviderQuotation() {
    final draft = _pendingProviderDraft;
    if (draft == null) {
      _error = 'There is no saved quotation to send.';
      notifyListeners();
      return Future.value(null);
    }

    return _runProviderQuotationSubmission(
      () => quotationService.sendProviderQuotation(draft.id),
    );
  }

  Future<QuotationModel?> _runProviderQuotationSubmission(
    Future<QuotationModel> Function() submit,
  ) async {
    final generation = _generation;
    if (!_isCurrent(generation)) return null;
    _isSubmitting = true;
    _error = null;
    notifyListeners();

    try {
      final sent = await submit();
      if (!_isCurrent(generation)) return null;
      _pendingProviderDraft = null;
      return sent;
    } catch (err) {
      if (!_isCurrent(generation)) return null;
      _error = quotationService.getErrorMessage(err);
      return null;
    } finally {
      if (_isCurrent(generation)) {
        _isSubmitting = false;
        notifyListeners();
      }
    }
  }

  /// Clears provider booking state (call on logout).
  void resetProviderBookings() {
    _providerBookings = [];
    _providerCompletedBookings = [];
    _providerBookingsError = null;
    _isProviderBookingsLoading = false;
    _pendingProviderDraft = null;
    notifyListeners();
  }

  // ─────────────────────────────────────────────────────────────
  // HELPERS
  // ─────────────────────────────────────────────────────────────

  void _updateInList(QuotationModel updated) {
    final idx = _quotations.indexWhere((q) => q.id == updated.id);
    if (idx >= 0) {
      _quotations[idx] = updated;
    } else {
      _quotations = [updated, ..._quotations];
    }
    if (_currentQuotation?.id == updated.id) {
      _currentQuotation = updated;
    }
  }

  void _replaceBookingLocally(int bookingId, BookingStatus newStatus) {
    for (var i = 0; i < _providerBookings.length; i++) {
      final old = _providerBookings[i];
      if (old.id != bookingId.toString()) continue;

      final updated = ProviderBookingSummaryModel(
        id: old.id,
        serviceRequestId: old.serviceRequestId,
        providerId: old.providerId,
        customerName: old.customerName,
        serviceTitle: old.serviceTitle,
        locationText: old.locationText,
        amount: old.amount,
        statusLabel: newStatus.displayLabel.toUpperCase(),
        statusCode: newStatus.code,
        scheduledAt: old.scheduledAt,
        isCompleted: newStatus == BookingStatus.completed,
        isPendingQuote: old.isPendingQuote,
      );

      if (newStatus == BookingStatus.completed) {
        _providerBookings.removeAt(i);
        _providerCompletedBookings = [updated, ..._providerCompletedBookings];
      } else {
        _providerBookings[i] = updated;
      }
      return;
    }
  }
}
