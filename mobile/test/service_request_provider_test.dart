import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/api/api_client.dart';
import 'package:mobile/features/service_requests/models/create_service_request_dto.dart';
import 'package:mobile/features/service_requests/models/problem_understanding_result_model.dart';
import 'package:mobile/features/service_requests/models/service_request_model.dart';
import 'package:mobile/features/service_requests/models/service_request_status.dart';
import 'package:mobile/features/service_requests/models/service_request_urgency.dart';
import 'package:mobile/features/service_requests/models/update_service_request_dto.dart';
import 'package:mobile/features/service_requests/providers/service_request_provider.dart';
import 'package:mobile/features/service_requests/services/service_request_service.dart';

class FakeServiceRequestService extends ServiceRequestService {
  FakeServiceRequestService() : super(apiClient: ApiClient());

  List<ServiceRequestModel> fakeRequests = [];
  ProblemUnderstandingResultModel? fakeAnalysis;
  bool shouldThrow = false;
  bool shouldAnalyzeThrow = false;
  Exception? analyzeException;
  bool shouldGetByIdThrow = false;
  int getByIdCallCount = 0;
  int analyzeCallCount = 0;
  Completer<ProblemUnderstandingResultModel>? analyzeCompleter;
  List<ServiceRequestModel>? getByIdResponses;

  @override
  Future<List<ServiceRequestModel>> getMyRequests() async {
    if (shouldThrow) throw Exception('Network error');
    return List.of(fakeRequests);
  }

  @override
  Future<ServiceRequestModel> getById(String id) async {
    getByIdCallCount++;
    if (shouldGetByIdThrow) throw Exception('Failed to refresh');
    if (shouldThrow && !shouldAnalyzeThrow) throw Exception('Request not found');
    if (getByIdResponses != null && getByIdResponses!.isNotEmpty) {
      return getByIdResponses!.removeAt(0);
    }
    return fakeRequests.firstWhere((r) => r.serviceRequestId == id);
  }

  @override
  Future<ServiceRequestModel> create(CreateServiceRequestDto dto) async {
    if (shouldThrow) throw Exception('Create failed');
    final created = ServiceRequestModel(
      serviceRequestId: 'req-new',
      customerId: 'cust-1',
      category: 'General',
      description: dto.description,
      locationText: dto.locationText,
      latitude: dto.latitude,
      longitude: dto.longitude,
      urgency: ServiceRequestUrgency.unknown,
      status: ServiceRequestStatus.created,
      createdAt: DateTime.now(),
      updatedAt: DateTime.now(),
    );
    fakeRequests.insert(0, created);
    return created;
  }

  @override
  Future<ServiceRequestModel> update(String id, UpdateServiceRequestDto dto) async {
    if (shouldThrow) throw Exception('Update failed');
    final index = fakeRequests.indexWhere((r) => r.serviceRequestId == id);
    final existing = fakeRequests[index];
    final updated = existing.copyWith(
      description: dto.description,
      locationText: dto.locationText,
    );
    fakeRequests[index] = updated;
    return updated;
  }

  @override
  Future<ServiceRequestModel> cancel(String id) async {
    if (shouldThrow) throw Exception('Cancel failed');
    final index = fakeRequests.indexWhere((r) => r.serviceRequestId == id);
    final cancelled = fakeRequests[index].copyWith(
      status: ServiceRequestStatus.cancelled,
    );
    fakeRequests[index] = cancelled;
    return cancelled;
  }

  @override
  Future<ProblemUnderstandingResultModel> analyze(String id) async {
    analyzeCallCount++;
    if (analyzeCompleter != null) {
      return await analyzeCompleter!.future;
    }
    if (analyzeException != null) throw analyzeException!;
    if (shouldThrow || shouldAnalyzeThrow) throw Exception('Analysis failed');
    return fakeAnalysis ??
        ProblemUnderstandingResultModel(
          workflowId: 'wf-1',
          executionId: 'ex-1',
          serviceRequestId: id,
          status: ServiceRequestStatus.analyzed,
          category: 'Plumbing',
          problemSummary: 'Pipe leakage in bathroom',
          urgency: ServiceRequestUrgency.high,
          confidence: 0.92,
          needsMoreInformation: false,
          followUpQuestions: const [],
        );
  }

  @override
  Future<ServiceRequestModel> markReadyForMatching(String id) async {
    if (shouldThrow) throw Exception('Mark ready failed');
    final index = fakeRequests.indexWhere((r) => r.serviceRequestId == id);
    final updated = fakeRequests[index].copyWith(
      status: ServiceRequestStatus.readyForMatching,
    );
    fakeRequests[index] = updated;
    return updated;
  }

  @override
  String getErrorMessage(Object error) {
    return 'Mock error: ${error.toString()}';
  }
}

void main() {
  late FakeServiceRequestService fakeService;
  late ServiceRequestProvider provider;

  final sampleRequest = ServiceRequestModel(
    serviceRequestId: 'req-1',
    customerId: 'cust-1',
    category: 'Electrical',
    description: 'Power cut in master bedroom',
    locationText: 'Colombo 07',
    urgency: ServiceRequestUrgency.medium,
    status: ServiceRequestStatus.created,
    createdAt: DateTime.now(),
    updatedAt: DateTime.now(),
  );

  setUp(() {
    fakeService = FakeServiceRequestService();
    fakeService.fakeRequests = [sampleRequest];
    provider = ServiceRequestProvider(
      serviceRequestService: fakeService,
      reconciliationPollInterval: Duration.zero,
      maxReconciliationPolls: 3,
    );
  });

  test('loadMyRequests updates requests list', () async {
    final success = await provider.loadMyRequests();

    expect(success, true);
    expect(provider.requests.length, 1);
    expect(provider.requests.first.serviceRequestId, 'req-1');
    expect(provider.isLoading, false);
    expect(provider.error, isNull);
  });

  test('createRequest adds new request to list and sets currentRequest', () async {
    await provider.loadMyRequests();
    const dto = CreateServiceRequestDto(
      description: 'New issue with sink',
      locationText: 'Colombo 03',
    );

    final created = await provider.createRequest(dto);

    expect(created, isNotNull);
    expect(provider.requests.length, 2);
    expect(provider.currentRequest?.serviceRequestId, 'req-new');
    expect(provider.requests.first.serviceRequestId, 'req-new');
  });

  test('cancelRequest updates status to cancelled', () async {
    await provider.loadMyRequests();
    final cancelled = await provider.cancelRequest('req-1');

    expect(cancelled, isNotNull);
    expect(cancelled?.status, ServiceRequestStatus.cancelled);
    expect(provider.requests.first.status, ServiceRequestStatus.cancelled);
  });

  test('analyzeRequest sets currentAnalysis and updates request status', () async {
    await provider.loadRequestById('req-1');
    final analysis = await provider.analyzeRequest('req-1');

    expect(analysis, isNotNull);
    expect(provider.currentAnalysis?.problemSummary, 'Pipe leakage in bathroom');
    expect(provider.currentRequest?.status, ServiceRequestStatus.analyzed);
    expect(provider.currentRequest?.urgency, ServiceRequestUrgency.high);
  });

  test('markReadyForMatching updates status to readyForMatching', () async {
    await provider.loadRequestById('req-1');
    final updated = await provider.markReadyForMatching('req-1');

    expect(updated, isNotNull);
    expect(updated?.status, ServiceRequestStatus.readyForMatching);
    expect(provider.currentRequest?.status, ServiceRequestStatus.readyForMatching);
  });

  test('handles service errors gracefully', () async {
    fakeService.shouldThrow = true;

    final success = await provider.loadMyRequests();

    expect(success, false);
    expect(provider.error, isNotNull);
    expect(provider.isLoading, false);
  });

  test('analyzeRequest_WhenAlreadyAnalyzing_DoesNotStartSecondRequest', () async {
    await provider.loadRequestById('req-1');
    fakeService.analyzeCompleter = Completer<ProblemUnderstandingResultModel>();

    // First call initiates analysis
    final firstFuture = provider.analyzeRequest('req-1');
    expect(provider.isAnalyzing, true);

    // Second call while already analyzing should be dropped (returns null, does not re-invoke service)
    final secondFuture = provider.analyzeRequest('req-1');
    final secondResult = await secondFuture;

    expect(secondResult, isNull);
    expect(fakeService.analyzeCallCount, 1);

    // Complete the first call
    final resultModel = ProblemUnderstandingResultModel(
      workflowId: 'wf-1',
      executionId: 'ex-1',
      serviceRequestId: 'req-1',
      status: ServiceRequestStatus.analyzed,
      category: 'Plumbing',
      problemSummary: 'Pipe leakage in bathroom',
      urgency: ServiceRequestUrgency.high,
      confidence: 0.92,
      needsMoreInformation: false,
      followUpQuestions: const [],
    );
    fakeService.analyzeCompleter!.complete(resultModel);
    final firstResult = await firstFuture;

    expect(firstResult, isNotNull);
    expect(provider.isAnalyzing, false);
    expect(fakeService.analyzeCallCount, 1);
  });

  test('Rapid double invocation results in only one service.analyze call', () async {
    await provider.loadRequestById('req-1');
    fakeService.analyzeCompleter = Completer<ProblemUnderstandingResultModel>();

    final future1 = provider.analyzeRequest('req-1');
    final future2 = provider.analyzeRequest('req-1');

    expect(fakeService.analyzeCallCount, 1);

    fakeService.analyzeCompleter!.complete(ProblemUnderstandingResultModel(
      workflowId: 'wf-1',
      executionId: 'ex-1',
      serviceRequestId: 'req-1',
      status: ServiceRequestStatus.analyzed,
      category: 'Plumbing',
      problemSummary: 'Pipe leakage in bathroom',
      urgency: ServiceRequestUrgency.high,
      confidence: 0.92,
      needsMoreInformation: false,
      followUpQuestions: const [],
    ));

    final results = await Future.wait([future1, future2]);
    expect(results[0], isNotNull);
    expect(results[1], isNull);
    expect(fakeService.analyzeCallCount, 1);
  });

  test('analyzeRequest_WhenTimeoutOccurs_RefreshesRequestFromBackend', () async {
    await provider.loadRequestById('req-1');
    fakeService.analyzeException = DioException(
      requestOptions: RequestOptions(path: '/service-requests/req-1/analyze'),
      type: DioExceptionType.receiveTimeout,
    );
    final initialGetCount = fakeService.getByIdCallCount;

    final result = await provider.analyzeRequest('req-1');

    expect(result, isNull);
    expect(fakeService.getByIdCallCount, greaterThan(initialGetCount));
    expect(provider.error, isNull);
    expect(provider.currentRequest?.status, ServiceRequestStatus.created);
    expect(provider.isAnalyzing, false);
  });

  test('analyzeRequest_When409Occurs_RefreshesRequestFromBackend', () async {
    await provider.loadRequestById('req-1');
    fakeService.analyzeException = DioException(
      requestOptions: RequestOptions(path: '/service-requests/req-1/analyze'),
      response: Response(
        requestOptions: RequestOptions(path: '/service-requests/req-1/analyze'),
        statusCode: 409,
      ),
    );
    final initialGetCount = fakeService.getByIdCallCount;

    final result = await provider.analyzeRequest('req-1');

    expect(result, isNull);
    expect(fakeService.getByIdCallCount, greaterThan(initialGetCount));
    expect(provider.error, 'Service request is currently being analyzed or in an updated status. The request status has been refreshed.');
    expect(provider.isAnalyzing, false);
  });

  test('Refreshed backend status Analyzing updates currentRequest', () async {
    await provider.loadRequestById('req-1');
    fakeService.fakeRequests = [
      sampleRequest.copyWith(status: ServiceRequestStatus.analyzing),
    ];
    fakeService.analyzeException = DioException(
      requestOptions: RequestOptions(path: '/service-requests/req-1/analyze'),
      type: DioExceptionType.receiveTimeout,
    );

    await provider.analyzeRequest('req-1');

    expect(provider.currentRequest?.status, ServiceRequestStatus.analyzing);
    expect(provider.requests.first.status, ServiceRequestStatus.analyzing);
    expect(provider.isAnalyzing, false);
    expect(provider.error, isNull);
  });

  test('isAnalyzing always resets safely after success, timeout, 409, and failed synchronization', () async {
    await provider.loadRequestById('req-1');

    // 1. Success
    await provider.analyzeRequest('req-1');
    expect(provider.isAnalyzing, false);

    // 2. Timeout
    fakeService.analyzeException = DioException(
      requestOptions: RequestOptions(path: '/service-requests/req-1/analyze'),
      type: DioExceptionType.receiveTimeout,
    );
    await provider.analyzeRequest('req-1');
    expect(provider.isAnalyzing, false);

    // 3. 409 Conflict
    fakeService.analyzeException = DioException(
      requestOptions: RequestOptions(path: '/service-requests/req-1/analyze'),
      response: Response(
        requestOptions: RequestOptions(path: '/service-requests/req-1/analyze'),
        statusCode: 409,
      ),
    );
    await provider.analyzeRequest('req-1');
    expect(provider.isAnalyzing, false);

    // 4. Synchronization itself fails - original analysis error is preserved
    fakeService.shouldGetByIdThrow = true;
    fakeService.analyzeException = DioException(
      requestOptions: RequestOptions(path: '/service-requests/req-1/analyze'),
      type: DioExceptionType.receiveTimeout,
    );
    await provider.analyzeRequest('req-1');
    expect(provider.isAnalyzing, false);
    expect(provider.error, 'Analysis is taking longer than expected. The request status has been refreshed.');
  });

  test('serviceRequestService getErrorMessage formats generic CRUD errors properly', () {
    final service = ServiceRequestService(apiClient: ApiClient());

    final timeoutError = DioException(
      requestOptions: RequestOptions(path: '/service-requests'),
      type: DioExceptionType.receiveTimeout,
    );
    expect(service.getErrorMessage(timeoutError), 'Request timed out. Please try again.');

    final conflictError = DioException(
      requestOptions: RequestOptions(path: '/service-requests'),
      response: Response(
        requestOptions: RequestOptions(path: '/service-requests'),
        statusCode: 409,
      ),
    );
    expect(service.getErrorMessage(conflictError), 'The request could not be completed due to a conflict.');
  });

  test('serviceRequestService getAnalysisErrorMessage formats timeout and conflict errors properly', () {
    final service = ServiceRequestService(apiClient: ApiClient());

    final timeoutError = DioException(
      requestOptions: RequestOptions(path: '/service-requests/1/analyze'),
      type: DioExceptionType.receiveTimeout,
    );
    expect(service.getAnalysisErrorMessage(timeoutError), contains('taking longer than expected'));
    expect(service.getAnalysisErrorMessage(timeoutError), contains('refreshed'));

    final conflictError = DioException(
      requestOptions: RequestOptions(path: '/service-requests/1/analyze'),
      response: Response(
        requestOptions: RequestOptions(path: '/service-requests/1/analyze'),
        statusCode: 409,
      ),
    );
    expect(service.getAnalysisErrorMessage(conflictError), contains('currently being analyzed'));
    expect(service.getAnalysisErrorMessage(conflictError), contains('refreshed'));
  });

  test('analysis fails and synchronization fails sets analysisStateNeedsRefresh to true', () async {
    await provider.loadRequestById('req-1');
    expect(provider.analysisStateNeedsRefresh, false);

    fakeService.shouldGetByIdThrow = true;
    fakeService.analyzeException = DioException(
      requestOptions: RequestOptions(path: '/service-requests/req-1/analyze'),
      type: DioExceptionType.receiveTimeout,
    );

    final result = await provider.analyzeRequest('req-1');

    expect(result, isNull);
    expect(provider.isAnalyzing, false);
    expect(provider.analysisStateNeedsRefresh, true);
    expect(provider.error, 'Analysis is taking longer than expected. The request status has been refreshed.');

    // While analysisStateNeedsRefresh == true, subsequent analyzeRequest calls are blocked
    final callCountBefore = fakeService.analyzeCallCount;
    final blockedResult = await provider.analyzeRequest('req-1');
    expect(blockedResult, isNull);
    expect(fakeService.analyzeCallCount, callCountBefore);
  });

  test('subsequent successful refresh clears analysisStateNeedsRefresh and updates currentRequest', () async {
    await provider.loadRequestById('req-1');

    // Cause synchronization failure
    fakeService.shouldGetByIdThrow = true;
    fakeService.analyzeException = DioException(
      requestOptions: RequestOptions(path: '/service-requests/req-1/analyze'),
      type: DioExceptionType.receiveTimeout,
    );
    await provider.analyzeRequest('req-1');
    expect(provider.analysisStateNeedsRefresh, true);

    // Backend is now reachable with updated status
    fakeService.shouldGetByIdThrow = false;
    fakeService.fakeRequests = [
      sampleRequest.copyWith(status: ServiceRequestStatus.analyzing),
    ];

    final refreshed = await provider.loadRequestById('req-1');

    expect(refreshed, isNotNull);
    expect(provider.analysisStateNeedsRefresh, false);
    expect(provider.currentRequest?.status, ServiceRequestStatus.analyzing);
  });

  test('Analyze timeout + immediate GET returns Analyzed synchronizes final state without fake analysis', () async {
    await provider.loadRequestById('req-1');
    fakeService.fakeRequests = [
      sampleRequest.copyWith(
        status: ServiceRequestStatus.analyzed,
        category: 'Plumbing',
        urgency: ServiceRequestUrgency.high,
        categoryHint: 'Electrical',
      ),
    ];
    fakeService.analyzeException = DioException(
      requestOptions: RequestOptions(path: '/service-requests/req-1/analyze'),
      type: DioExceptionType.receiveTimeout,
    );

    final result = await provider.analyzeRequest('req-1');

    expect(result, isNull);
    expect(provider.currentRequest?.status, ServiceRequestStatus.analyzed);
    expect(provider.currentRequest?.category, 'Plumbing');
    expect(provider.currentRequest?.urgency, ServiceRequestUrgency.high);
    expect(provider.currentRequest?.categoryHint, 'Electrical');
    expect(provider.currentAnalysis, isNull); // No fake/synthesized analysis fabricated
    expect(provider.error, isNull);
    expect(provider.analysisStateNeedsRefresh, false);
    expect(fakeService.analyzeCallCount, 1);
  });

  test('Analyze timeout + immediate GET returns AwaitingInformation synchronizes final state', () async {
    await provider.loadRequestById('req-1');
    fakeService.fakeRequests = [
      sampleRequest.copyWith(
        status: ServiceRequestStatus.awaitingInformation,
      ),
    ];
    fakeService.analyzeException = DioException(
      requestOptions: RequestOptions(path: '/service-requests/req-1/analyze'),
      type: DioExceptionType.receiveTimeout,
    );

    final result = await provider.analyzeRequest('req-1');

    expect(result, isNull);
    expect(provider.currentRequest?.status, ServiceRequestStatus.awaitingInformation);
    expect(provider.currentAnalysis, isNull);
    expect(provider.error, isNull);
    expect(provider.analysisStateNeedsRefresh, false);
  });

  test('Analyze timeout + first GET Analyzing + 2nd poll Analyzed succeeds with GET only', () async {
    await provider.loadRequestById('req-1');
    fakeService.analyzeException = DioException(
      requestOptions: RequestOptions(path: '/service-requests/req-1/analyze'),
      type: DioExceptionType.receiveTimeout,
    );
    fakeService.getByIdResponses = [
      sampleRequest.copyWith(status: ServiceRequestStatus.analyzing),
      sampleRequest.copyWith(status: ServiceRequestStatus.analyzed, category: 'Plumbing'),
    ];

    final result = await provider.analyzeRequest('req-1');

    expect(result, isNull);
    expect(provider.currentRequest?.status, ServiceRequestStatus.analyzed);
    expect(provider.currentRequest?.category, 'Plumbing');
    expect(provider.currentAnalysis, isNull);
    expect(provider.error, isNull);
    expect(provider.isAnalyzing, false);
    expect(fakeService.analyzeCallCount, 1); // GET only, no duplicate POST
  });

  test('Analyze timeout + first GET Analyzing + 2nd poll AwaitingInformation succeeds', () async {
    await provider.loadRequestById('req-1');
    fakeService.analyzeException = DioException(
      requestOptions: RequestOptions(path: '/service-requests/req-1/analyze'),
      type: DioExceptionType.receiveTimeout,
    );
    fakeService.getByIdResponses = [
      sampleRequest.copyWith(status: ServiceRequestStatus.analyzing),
      sampleRequest.copyWith(status: ServiceRequestStatus.awaitingInformation),
    ];

    await provider.analyzeRequest('req-1');

    expect(provider.currentRequest?.status, ServiceRequestStatus.awaitingInformation);
    expect(provider.error, isNull);
    expect(provider.isAnalyzing, false);
    expect(fakeService.analyzeCallCount, 1);
  });

  test('Analyze timeout + first GET Analyzing + 2nd poll Created (backend recovery) succeeds', () async {
    await provider.loadRequestById('req-1');
    fakeService.analyzeException = DioException(
      requestOptions: RequestOptions(path: '/service-requests/req-1/analyze'),
      type: DioExceptionType.receiveTimeout,
    );
    fakeService.getByIdResponses = [
      sampleRequest.copyWith(status: ServiceRequestStatus.analyzing),
      sampleRequest.copyWith(status: ServiceRequestStatus.created),
    ];

    await provider.analyzeRequest('req-1');

    expect(provider.currentRequest?.status, ServiceRequestStatus.created);
    expect(provider.error, isNull);
    expect(provider.isAnalyzing, false);
    expect(fakeService.analyzeCallCount, 1);
  });

  test('Polling stops immediately when status != Analyzing without exhausting limit', () async {
    await provider.loadRequestById('req-1');
    fakeService.analyzeException = DioException(
      requestOptions: RequestOptions(path: '/service-requests/req-1/analyze'),
      type: DioExceptionType.receiveTimeout,
    );
    final initialGetCount = fakeService.getByIdCallCount;
    fakeService.getByIdResponses = [
      sampleRequest.copyWith(status: ServiceRequestStatus.analyzing), // initial GET
      sampleRequest.copyWith(status: ServiceRequestStatus.analyzed), // poll 1
    ];

    await provider.analyzeRequest('req-1', maxPolls: 10);

    // initial GET (1) + poll 1 (1) = 2 GET calls, stopped immediately before poll 2..10
    expect(fakeService.getByIdCallCount - initialGetCount, 2);
    expect(provider.currentRequest?.status, ServiceRequestStatus.analyzed);
  });

  test('Polling is bounded and cannot run forever (grace-period expiry)', () async {
    await provider.loadRequestById('req-1');
    fakeService.fakeRequests = [
      sampleRequest.copyWith(status: ServiceRequestStatus.analyzing),
    ];
    fakeService.analyzeException = DioException(
      requestOptions: RequestOptions(path: '/service-requests/req-1/analyze'),
      type: DioExceptionType.receiveTimeout,
    );
    final initialGetCount = fakeService.getByIdCallCount;

    await provider.analyzeRequest('req-1', maxPolls: 4);

    // Initial GET (1) + 4 bounded polls = exactly 5 calls
    expect(fakeService.getByIdCallCount - initialGetCount, 5);
    expect(provider.currentRequest?.status, ServiceRequestStatus.analyzing);
    expect(provider.isAnalyzing, false);
    expect(provider.analysisStateNeedsRefresh, false);
    expect(provider.error, isNull); // Informational state handled by UI, not provider.error
  });

  test('Reconciliation GET network failure during polling sets analysisStateNeedsRefresh', () async {
    await provider.loadRequestById('req-1');
    fakeService.analyzeException = DioException(
      requestOptions: RequestOptions(path: '/service-requests/req-1/analyze'),
      type: DioExceptionType.receiveTimeout,
    );
    fakeService.getByIdResponses = [
      sampleRequest.copyWith(status: ServiceRequestStatus.analyzing), // initial GET
    ];

    // After initial GET, getById will throw
    final future = provider.analyzeRequest('req-1', maxPolls: 3);
    fakeService.shouldGetByIdThrow = true;
    await future;

    expect(provider.analysisStateNeedsRefresh, true);
    expect(provider.error, isNotNull);
    expect(provider.isAnalyzing, false);
  });

  test('CategoryHint survives reconciliation and refreshes intact', () async {
    final hintedRequest = sampleRequest.copyWith(
      categoryHint: 'Vehicle Repair',
      status: ServiceRequestStatus.created,
    );
    fakeService.fakeRequests = [hintedRequest];
    await provider.loadRequestById('req-1');

    fakeService.analyzeException = DioException(
      requestOptions: RequestOptions(path: '/service-requests/req-1/analyze'),
      type: DioExceptionType.receiveTimeout,
    );
    fakeService.getByIdResponses = [
      hintedRequest.copyWith(status: ServiceRequestStatus.analyzing),
      hintedRequest.copyWith(status: ServiceRequestStatus.analyzed, category: 'Plumbing'),
    ];

    await provider.analyzeRequest('req-1');

    expect(provider.currentRequest?.categoryHint, 'Vehicle Repair');
    expect(provider.currentRequest?.category, 'Plumbing');
  });

  test('Deterministic HTTP 400 does not enter reconciliation polling', () async {
    await provider.loadRequestById('req-1');
    fakeService.analyzeException = DioException(
      requestOptions: RequestOptions(path: '/service-requests/req-1/analyze'),
      response: Response(
        requestOptions: RequestOptions(path: '/service-requests/req-1/analyze'),
        statusCode: 400,
      ),
    );
    final initialGetCount = fakeService.getByIdCallCount;

    await provider.analyzeRequest('req-1', maxPolls: 5);

    // Exactly 1 synchronization GET, no polling
    expect(fakeService.getByIdCallCount - initialGetCount, 1);
    expect(provider.error, isNotNull);
    expect(provider.isAnalyzing, false);
  });
}
