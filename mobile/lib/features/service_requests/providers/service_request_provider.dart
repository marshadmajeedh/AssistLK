import 'package:flutter/foundation.dart';

import '../models/create_service_request_dto.dart';
import '../models/problem_understanding_result_model.dart';
import '../models/service_request_model.dart';
import '../models/update_service_request_dto.dart';
import '../services/service_request_service.dart';

class ServiceRequestProvider extends ChangeNotifier {
  final ServiceRequestService serviceRequestService;

  ServiceRequestProvider({required this.serviceRequestService});

  List<ServiceRequestModel> _requests = [];
  ServiceRequestModel? _currentRequest;
  // Backend currently does not persist followUpQuestions.
  // Stored temporarily until customer completes clarification.
  ProblemUnderstandingResultModel? _currentAnalysis;
  bool _isLoading = false;
  bool _isAnalyzing = false;
  String? _error;

  List<ServiceRequestModel> get requests => List.unmodifiable(_requests);
  ServiceRequestModel? get currentRequest => _currentRequest;
  ProblemUnderstandingResultModel? get currentAnalysis => _currentAnalysis;
  bool get isLoading => _isLoading;
  bool get isAnalyzing => _isAnalyzing;
  String? get error => _error;

  void clearError() {
    _error = null;
    notifyListeners();
  }

  void setCurrentRequest(ServiceRequestModel? request) {
    _currentRequest = request;
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

  Future<ProblemUnderstandingResultModel?> analyzeRequest(String id) async {
    _setAnalyzing(true);
    _error = null;

    try {
      final result = await serviceRequestService.analyze(id);
      _currentAnalysis = result;

      // Update the current request's status, category, urgency if available
      if (_currentRequest?.serviceRequestId == id) {
        _currentRequest = _currentRequest!.copyWith(
          status: result.status,
          category: result.category,
          urgency: result.urgency,
        );
        _updateRequestInList(_currentRequest!);
      }

      return result;
    } catch (err) {
      _error = serviceRequestService.getErrorMessage(err);
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
