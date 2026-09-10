import 'dart:async';

import 'package:flutter/foundation.dart';

import '../models/create_service_request_dto.dart';
import '../models/problem_understanding_result_model.dart';
import '../models/service_request_model.dart';
import '../models/service_request_status.dart';
import '../models/update_service_request_dto.dart';
import '../services/service_request_service.dart';

class ServiceRequestProvider extends ChangeNotifier {
  final ServiceRequestService serviceRequestService;
  final Duration reconciliationPollInterval;
  final int maxReconciliationPolls;

  ServiceRequestProvider({
    required this.serviceRequestService,
    this.reconciliationPollInterval = const Duration(seconds: 2),
    this.maxReconciliationPolls = 12,
  });

  List<ServiceRequestModel> _requests = [];
  ServiceRequestModel? _currentRequest;
  // Backend currently does not persist followUpQuestions.
  // Stored temporarily until customer completes clarification.
  ProblemUnderstandingResultModel? _currentAnalysis;
  bool _isLoading = false;
  bool _isAnalyzing = false;
  bool _analysisStateNeedsRefresh = false;
  String? _error;
  bool _isDisposed = false;

  @override
  void dispose() {
    _isDisposed = true;
    super.dispose();
  }

  @override
  void notifyListeners() {
    if (!_isDisposed) {
      super.notifyListeners();
    }
  }

  List<ServiceRequestModel> get requests => List.unmodifiable(_requests);
  ServiceRequestModel? get currentRequest => _currentRequest;
  ProblemUnderstandingResultModel? get currentAnalysis => _currentAnalysis;
  bool get isLoading => _isLoading;
  bool get isAnalyzing => _isAnalyzing;
  bool get analysisStateNeedsRefresh => _analysisStateNeedsRefresh;
  String? get error => _error;
  bool get isDisposed => _isDisposed;

  void clearError() {
    _error = null;
    notifyListeners();
  }

  void setCurrentRequest(ServiceRequestModel? request) {
    _currentRequest = request;
    _analysisStateNeedsRefresh = false;
    notifyListeners();
  }

  void setCurrentAnalysis(ProblemUnderstandingResultModel? analysis) {
    _currentAnalysis = analysis;
    notifyListeners();
  }

  Future<bool> loadMyRequests() async {
    _setLoading(true);
    _error = null;

    try {
      final fetched = await serviceRequestService.getMyRequests();
      _requests = List<ServiceRequestModel>.from(fetched);

      // Update currentRequest if it exists in the new list
      if (_currentRequest != null) {
        final match = _requests.where(
          (r) => r.serviceRequestId == _currentRequest!.serviceRequestId,
        );
        if (match.isNotEmpty) {
          _currentRequest = match.first;
        }
      }

      return true;
    } catch (err) {
      _error = serviceRequestService.getErrorMessage(err);
      return false;
    } finally {
      _setLoading(false);
    }
  }

  Future<ServiceRequestModel?> loadRequestById(String id) async {
    _setLoading(true);
    _error = null;

    try {
      final request = await serviceRequestService.getById(id);
      _currentRequest = request;
      _updateRequestInList(request);
      _analysisStateNeedsRefresh = false;
      return request;
    } catch (err) {
      _error = serviceRequestService.getErrorMessage(err);
      return null;
    } finally {
      _setLoading(false);
    }
  }

  Future<ServiceRequestModel?> createRequest(CreateServiceRequestDto dto) async {
    final validationError = dto.validate();
    if (validationError != null) {
      _error = validationError;
      notifyListeners();
      return null;
    }

    _setLoading(true);
    _error = null;

    try {
      final created = await serviceRequestService.create(dto);
      _requests = [created, ..._requests];
      _currentRequest = created;
      _currentAnalysis = null;
      return created;
    } catch (err) {
      _error = serviceRequestService.getErrorMessage(err);
      return null;
    } finally {
      _setLoading(false);
    }
  }

  Future<ServiceRequestModel?> updateRequest(
    String id,
    UpdateServiceRequestDto dto,
  ) async {
    final validationError = dto.validate();
    if (validationError != null) {
      _error = validationError;
      notifyListeners();
      return null;
    }

    _setLoading(true);
    _error = null;

    try {
      final updated = await serviceRequestService.update(id, dto);
      _updateRequestInList(updated);
      if (_currentRequest?.serviceRequestId == id) {
        _currentRequest = updated;
      }
      return updated;
    } catch (err) {
      _error = serviceRequestService.getErrorMessage(err);
      return null;
    } finally {
      _setLoading(false);
    }
  }

  Future<ServiceRequestModel?> cancelRequest(String id) async {
    _setLoading(true);
    _error = null;

    try {
      final cancelled = await serviceRequestService.cancel(id);
      _updateRequestInList(cancelled);
      if (_currentRequest?.serviceRequestId == id) {
        _currentRequest = cancelled;
      }
      return cancelled;
    } catch (err) {
      _error = serviceRequestService.getErrorMessage(err);
      return null;
    } finally {
      _setLoading(false);
    }
  }

  Future<ProblemUnderstandingResultModel?> analyzeRequest(
    String id, {
    Duration? pollInterval,
    int? maxPolls,
  }) async {
    if (_isAnalyzing || _analysisStateNeedsRefresh) {
      return null;
    }

    _setAnalyzing(true);
    _error = null;

    try {
      final result = await serviceRequestService.analyze(id);
      _currentAnalysis = result;
      _analysisStateNeedsRefresh = false;

      // Update the current request's status, category, urgency if available
      if (_currentRequest?.serviceRequestId == id) {
        _currentRequest = _currentRequest!.copyWith(
          status: result.status,
          category: result.category,
          urgency: result.urgency,
        );
        _updateRequestInList(_currentRequest!);
      } else {
        final index = _requests.indexWhere((r) => r.serviceRequestId == id);
        if (index >= 0) {
          final updated = _requests[index].copyWith(
            status: result.status,
            category: result.category,
            urgency: result.urgency,
          );
          _requests[index] = updated;
        }
      }

      return result;
    } catch (err) {
      final analysisError = serviceRequestService.getAnalysisErrorMessage(err);
      final isTimeoutOrUncertain =
          serviceRequestService.isTimeoutOrUncertainTransport(err);

      if (!isTimeoutOrUncertain) {
        // Deterministic HTTP/API error (e.g. 400, 403, 404, 409, 500)
        // Attempt one authoritative GET to synchronize state
        try {
          final refreshed = await serviceRequestService.getById(id);
          _currentRequest = refreshed;
          _updateRequestInList(refreshed);
          _analysisStateNeedsRefresh = false;
        } catch (_) {
          _analysisStateNeedsRefresh = true;
        }

        _error = analysisError;
        return null;
      }

      // Timeout / uncertain transport outcome where server completion is unknown:
      // 1. Initial authoritative GET
      ServiceRequestModel refreshed;
      try {
        refreshed = await serviceRequestService.getById(id);
      } catch (_) {
        // Authoritative GET itself failed -> state is genuinely uncertain
        _analysisStateNeedsRefresh = true;
        _error = analysisError;
        return null;
      }

      _currentRequest = refreshed;
      _updateRequestInList(refreshed);
      _analysisStateNeedsRefresh = false;
      _error = null; // Backend reachable; clear transport timeout error

      // 2. If status has already transitioned away from Analyzing, stop immediately
      if (refreshed.status != ServiceRequestStatus.analyzing) {
        return null;
      }

      // 3. Status is still Analyzing -> enter bounded reconciliation polling (GET only)
      final interval = pollInterval ?? reconciliationPollInterval;
      final limit = maxPolls ?? maxReconciliationPolls;
      int pollCount = 0;

      while (pollCount < limit) {
        pollCount++;
        if (interval > Duration.zero) {
          await Future.delayed(interval);
        }

        if (_isDisposed || _currentRequest?.serviceRequestId != id) {
          return null;
        }

        try {
          final pollRefreshed = await serviceRequestService.getById(id);
          _currentRequest = pollRefreshed;
          _updateRequestInList(pollRefreshed);
          _error = null;

          if (pollRefreshed.status != ServiceRequestStatus.analyzing) {
            _analysisStateNeedsRefresh = false;
            return null;
          }
        } catch (pollErr) {
          // If polling GET becomes unreliable, mark state as needing refresh
          _analysisStateNeedsRefresh = true;
          _error = serviceRequestService.getErrorMessage(pollErr);
          return null;
        }
      }

      // 4. Grace period expired while backend is still Analyzing:
      // Leave authoritative request status as Analyzing,
      // clear error (informational UI is driven by status/isAnalyzing/analysisStateNeedsRefresh),
      // analysisStateNeedsRefresh remains false because backend was reached.
      _analysisStateNeedsRefresh = false;
      _error = null;
      return null;
    } finally {
      _setAnalyzing(false);
    }
  }

  Future<ServiceRequestModel?> markReadyForMatching(String id) async {
    _setLoading(true);
    _error = null;

    try {
      final updated = await serviceRequestService.markReadyForMatching(id);
      _updateRequestInList(updated);
      if (_currentRequest?.serviceRequestId == id) {
        _currentRequest = updated;
      }
      return updated;
    } catch (err) {
      _error = serviceRequestService.getErrorMessage(err);
      return null;
    } finally {
      _setLoading(false);
    }
  }

  void reset() {
    _requests = [];
    _currentRequest = null;
    _currentAnalysis = null;
    _isLoading = false;
    _isAnalyzing = false;
    _analysisStateNeedsRefresh = false;
    _error = null;
    notifyListeners();
  }

  void _updateRequestInList(ServiceRequestModel updated) {
    final index = _requests.indexWhere(
      (r) => r.serviceRequestId == updated.serviceRequestId,
    );
    if (index >= 0) {
      _requests[index] = updated;
    } else {
      _requests.add(updated);
    }
  }

  void _setLoading(bool value) {
    _isLoading = value;
    notifyListeners();
  }

  void _setAnalyzing(bool value) {
    _isAnalyzing = value;
    notifyListeners();
  }
}
