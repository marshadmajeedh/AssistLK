import 'package:flutter/foundation.dart';

import '../models/booking_model.dart';
import '../models/quotation_model.dart';
import '../models/quotation_status.dart';
import '../services/quotation_service.dart';

class QuotationProvider extends ChangeNotifier {
  final QuotationService quotationService;

  QuotationProvider({required this.quotationService});

  List<QuotationModel> _quotations = [];
  QuotationModel? _currentQuotation;
  BookingModel? _lastBooking;
  bool _isLoading = false;
  bool _isSubmitting = false;
  String? _error;

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

  List<QuotationModel> get quotations => List.unmodifiable(_quotations);
  QuotationModel? get currentQuotation => _currentQuotation;
  BookingModel? get lastBooking => _lastBooking;
  bool get isLoading => _isLoading;
  bool get isSubmitting => _isSubmitting;
  String? get error => _error;

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
    notifyListeners();
  }

  Future<bool> loadForServiceRequest(String serviceRequestId) async {
    final generation = _generation;
    if (!_isCurrent(generation)) return false;
    _isLoading = true;
    _error = null;
    notifyListeners();

    try {
      final fetched =
          await quotationService.listForServiceRequest(serviceRequestId);
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
        _quotations[matchIndex] =
            _quotations[matchIndex].copyWith(status: QuotationStatus.approved);
      }
      if (_currentQuotation?.id == quotationId) {
        _currentQuotation =
            _currentQuotation!.copyWith(status: QuotationStatus.approved);
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
}