import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../models/create_service_request_dto.dart';
import '../models/problem_understanding_result_model.dart';
import '../models/service_request_model.dart';
import '../models/service_request_status.dart';
import '../models/submit_clarification_answers_dto.dart';
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
  ProblemUnderstandingResultModel? _currentAnalysis;
  Map<String, dynamic>? _lastValidationResult;
  Map<String, dynamic>? _lastSentimentResult;
  bool _isLoading = false;
  bool _isAnalyzing = false;
  bool _analysisStateNeedsRefresh = false;
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
    if (!_isDisposed) {
      super.notifyListeners();
    }
  }

  List<ServiceRequestModel> get requests => List.unmodifiable(_requests);
  ServiceRequestModel? get currentRequest => _currentRequest;
  ProblemUnderstandingResultModel? get currentAnalysis => _currentAnalysis;
  Map<String, dynamic>? get lastValidationResult => _lastValidationResult;
  Map<String, dynamic>? get lastSentimentResult => _lastSentimentResult;
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
    final generation = _generation;
    if (!_isCurrent(generation)) return false;
    _setLoading(true);
    _error = null;

    try {
      final fetched = await serviceRequestService.getMyRequests();
      if (!_isCurrent(generation)) return false;
      _requests = List<ServiceRequestModel>.from(fetched);

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
      if (!_isCurrent(generation)) return false;
      _error = serviceRequestService.getErrorMessage(err);
      return false;
    } finally {
      if (_isCurrent(generation)) _setLoading(false);
    }
  }

  Future<ServiceRequestModel?> loadRequestById(String id) async {
    final generation = _generation;
    if (!_isCurrent(generation)) return null;
    _setLoading(true);
    _error = null;

    try {
      final request = await serviceRequestService.getById(id);
      if (!_isCurrent(generation)) return null;
      _applyRequestUpdate(request);
      _analysisStateNeedsRefresh = false;
      return request;
    } catch (err) {
      if (!_isCurrent(generation)) return null;
      _error = serviceRequestService.getErrorMessage(err);
      return null;
    } finally {
      if (_isCurrent(generation)) _setLoading(false);
    }
  }

  Future<ServiceRequestModel?> createRequest(
    CreateServiceRequestDto dto,
  ) async {
    final generation = _generation;
    if (!_isCurrent(generation)) return null;
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
      if (!_isCurrent(generation)) return null;
      _requests = [created, ..._requests];
      _currentRequest = created;
      _currentAnalysis = null;
      return created;
    } catch (err) {
      if (!_isCurrent(generation)) return null;
      _error = serviceRequestService.getErrorMessage(err);
      return null;
    } finally {
      if (_isCurrent(generation)) _setLoading(false);
    }
  }

  Future<ServiceRequestModel?> updateRequest(
    String id,
    UpdateServiceRequestDto dto,
  ) async {
    final generation = _generation;
    if (!_isCurrent(generation)) return null;
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
      if (!_isCurrent(generation)) return null;
      _updateRequestInList(updated);
      if (_currentRequest?.serviceRequestId == id) {
        _currentRequest = updated;
      }
      return updated;
    } catch (err) {
      if (!_isCurrent(generation)) return null;
      _error = serviceRequestService.getErrorMessage(err);
      return null;
    } finally {
      if (_isCurrent(generation)) _setLoading(false);
    }
  }

  Future<ServiceRequestModel?> cancelRequest(String id) async {
    final generation = _generation;
    if (!_isCurrent(generation)) return null;
    _setLoading(true);
    _error = null;

    try {
      final cancelled = await serviceRequestService.cancel(id);
      if (!_isCurrent(generation)) return null;
      _updateRequestInList(cancelled);
      if (_currentRequest?.serviceRequestId == id) {
        _currentRequest = cancelled;
      }
      return cancelled;
    } catch (err) {
      if (!_isCurrent(generation)) return null;
      _error = serviceRequestService.getErrorMessage(err);
      return null;
    } finally {
      if (_isCurrent(generation)) _setLoading(false);
    }
  }

  Future<Map<String, dynamic>?> validateTransition({
    required String jobId,
    required String currentStatus,
    required String targetStatus,
    required double elapsedMinutes,
    String note = '',
  }) async {
    final generation = _generation;
    if (!_isCurrent(generation)) return null;
    _setAnalyzing(true);
    _error = null;

    try {
      final result = await serviceRequestService.validateTransition(
        jobId: jobId,
        currentStatus: currentStatus,
        targetStatus: targetStatus,
        elapsedMinutes: elapsedMinutes,
        note: note,
      );
      if (!_isCurrent(generation)) return null;

      _lastValidationResult = result;
      return result;
    } catch (err) {
      if (!_isCurrent(generation)) return null;
      _error = serviceRequestService.getAnalysisErrorMessage(err);
      return null;
    } finally {
      if (_isCurrent(generation)) _setAnalyzing(false);
    }
  }

  Future<ProblemUnderstandingResultModel?> analyzeRequest(
    String id, {
    Duration? pollInterval,
    int? maxPolls,
  }) async {
    final generation = _generation;
    if (!_isCurrent(generation)) return null;
    if (_isAnalyzing || _analysisStateNeedsRefresh) {
      return null;
    }

    _setAnalyzing(true);
    _error = null;

    try {
      final result = await serviceRequestService.analyze(id);
      if (!_isCurrent(generation)) return null;
      _currentAnalysis = result;

      try {
        final refreshed = await serviceRequestService.getById(id);
        if (!_isCurrent(generation)) return null;
        _applyRequestUpdate(refreshed);
        _analysisStateNeedsRefresh = false;
      } catch (_) {
        if (!_isCurrent(generation)) return null;
        _analysisStateNeedsRefresh = true;
      }

      return result;
    } catch (err) {
      if (!_isCurrent(generation)) return null;

      final shouldRefresh =
          serviceRequestService.isTimeoutOrUncertainTransport(err) ||
          (err is DioException && err.response?.statusCode == 409);

      if (shouldRefresh) {
        await _reconcileAnalysisState(
          id,
          err,
          generation: generation,
          pollInterval: pollInterval,
          maxPolls: maxPolls,
        );
      } else {
        try {
          final refreshed = await serviceRequestService.getById(id);
          if (!_isCurrent(generation)) return null;
          _applyRequestUpdate(refreshed);
          _analysisStateNeedsRefresh = false;
        } catch (_) {
          if (!_isCurrent(generation)) return null;
          _analysisStateNeedsRefresh = true;
        }
        _error = serviceRequestService.getAnalysisErrorMessage(err);
      }
      return null;
    } finally {
      if (_isCurrent(generation)) _setAnalyzing(false);
    }
  }

  Future<bool> updateRequestStatusToCompleted(String jobId) async {
    final generation = _generation;
    if (!_isCurrent(generation)) return false;
    _setLoading(true);
    _error = null;

    try {
      await serviceRequestService.updateRequestStatusToCompleted(jobId);
      if (!_isCurrent(generation)) return false;

      final completedRequest = _requests
          .cast<ServiceRequestModel?>()
          .firstWhere(
            (request) => request?.serviceRequestId == jobId,
            orElse: () => null,
          );
      if (completedRequest != null) {
        final updated = completedRequest.copyWith(
          status: ServiceRequestStatus.completed,
          updatedAt: DateTime.now(),
        );
        _updateRequestInList(updated);
        if (_currentRequest?.serviceRequestId == jobId) {
          _currentRequest = updated;
        }
      }
      notifyListeners();
      return true;
    } catch (err) {
      if (!_isCurrent(generation)) return false;
      _error = serviceRequestService.getErrorMessage(err);
      return false;
    } finally {
      if (_isCurrent(generation)) _setLoading(false);
    }
  }

  Future<void> _reconcileAnalysisState(
    String id,
    Object analysisError, {
    required int generation,
    Duration? pollInterval,
    int? maxPolls,
  }) async {
    final message = serviceRequestService.getAnalysisErrorMessage(
      analysisError,
    );
    final isConflict =
        analysisError is DioException &&
        analysisError.response?.statusCode == 409;

    try {
      final request = await serviceRequestService.getById(id);
      if (!_isCurrent(generation)) return;
      _applyRequestUpdate(request);

      if (request.status != ServiceRequestStatus.analyzing) {
        _analysisStateNeedsRefresh = false;
        _error = isConflict ? message : null;
        return;
      }

      final interval = pollInterval ?? reconciliationPollInterval;
      final polls = maxPolls ?? maxReconciliationPolls;

      for (var poll = 0; poll < polls; poll++) {
        if (interval > Duration.zero) {
          await Future<void>.delayed(interval);
          if (!_isCurrent(generation)) return;
        }

        if (_currentRequest?.serviceRequestId != id) {
          return;
        }

        try {
          final pollRefreshed = await serviceRequestService.getById(id);
          if (!_isCurrent(generation)) return;

          _applyRequestUpdate(pollRefreshed);
          _error = null;

          if (pollRefreshed.status != ServiceRequestStatus.analyzing) {
            _analysisStateNeedsRefresh = false;
            return;
          }
        } catch (pollErr) {
          if (!_isCurrent(generation)) return;
          _analysisStateNeedsRefresh = true;
          _error = serviceRequestService.getErrorMessage(pollErr);
          return;
        }
      }

      _analysisStateNeedsRefresh = false;
      _error = null;
    } catch (_) {
      if (!_isCurrent(generation)) return;
      _analysisStateNeedsRefresh = true;
      _error = message;
    }
  }

  void _applyRequestUpdate(ServiceRequestModel request) {
    _currentRequest = request;
    _updateRequestInList(request);
  }

  Future<Map<String, dynamic>?> analyzeSentiment(String feedbackText) async {
    final generation = _generation;
    if (!_isCurrent(generation)) return null;
    _setAnalyzing(true);
    _error = null;

    try {
      final result = await serviceRequestService.analyzeSentiment(feedbackText);
      if (!_isCurrent(generation)) return null;
      _lastSentimentResult = result;
      return result;
    } catch (err) {
      if (!_isCurrent(generation)) return null;
      _error = serviceRequestService.getAnalysisErrorMessage(err);
      return null;
    } finally {
      if (_isCurrent(generation)) _setAnalyzing(false);
    }
  }

  Future<ServiceRequestModel?> markReadyForMatching(String id) async {
    final generation = _generation;
    if (!_isCurrent(generation)) return null;
    _setLoading(true);
    _error = null;

    try {
      final updated = await serviceRequestService.markReadyForMatching(id);
      if (!_isCurrent(generation)) return null;
      _updateRequestInList(updated);
      if (_currentRequest?.serviceRequestId == id) {
        _currentRequest = updated;
      }
      return updated;
    } catch (err) {
      if (!_isCurrent(generation)) return null;
      _error = serviceRequestService.getErrorMessage(err);
      return null;
    } finally {
      if (_isCurrent(generation)) _setLoading(false);
    }
  }

  Future<bool> submitClarificationAnswers(
    String id,
    int round,
    Map<String, String> answers,
  ) async {
    final generation = _generation;
    if (!_isCurrent(generation)) return false;
    _setLoading(true);
    _error = null;

    try {
      final submission = SubmitClarificationAnswersDto(
        clarificationRound: round,
        answers: answers.entries
            .map(
              (e) => ClarificationAnswerSubmissionItemDto(
                clarificationId: e.key,
                answer: e.value,
              ),
            )
            .toList(),
      );

      final updatedClarifications = await serviceRequestService
          .submitClarificationAnswers(id, submission);
      if (!_isCurrent(generation)) return false;

      if (_currentRequest != null && _currentRequest!.serviceRequestId == id) {
        _currentRequest = _currentRequest!.copyWith(
          clarifications: updatedClarifications,
        );
        _updateRequestInList(_currentRequest!);
      }

      return true;
    } catch (err) {
      if (!_isCurrent(generation)) return false;
      _error = serviceRequestService.getErrorMessage(err);
      return false;
    } finally {
      if (_isCurrent(generation)) _setLoading(false);
    }
  }

  Future<ProblemUnderstandingResultModel?>
  submitClarificationAnswersAndReanalyze(
    String id,
    int round,
    Map<String, String> answers, {
    Duration? pollInterval,
    int? maxPolls,
  }) async {
    final generation = _generation;
    if (!_isCurrent(generation)) return null;
    final success = await submitClarificationAnswers(id, round, answers);
    if (!_isCurrent(generation)) return null;
    if (!success) {
      return null;
    }
    return analyzeRequest(id, pollInterval: pollInterval, maxPolls: maxPolls);
  }

  void reset() {
    _generation++;
    _requests = [];
    _currentRequest = null;
    _currentAnalysis = null;
    _lastValidationResult = null;
    _lastSentimentResult = null;
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
