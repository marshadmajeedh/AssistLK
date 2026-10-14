import 'package:mobile/features/service_requests/models/service_request_attachment.dart';
import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/api/api_client.dart';
import 'package:mobile/features/service_requests/models/create_service_request_dto.dart';
import 'package:mobile/features/service_requests/models/problem_understanding_result_model.dart';
import 'package:mobile/features/service_requests/models/service_request_clarification_model.dart';
import 'package:mobile/features/service_requests/models/service_request_model.dart';
import 'package:mobile/features/service_requests/models/service_request_status.dart';
import 'package:mobile/features/service_requests/models/service_request_urgency.dart';
import 'package:mobile/features/service_requests/models/submit_clarification_answers_dto.dart';
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
  Future<List<ServiceRequestAttachment>> listAttachments(String id, {CancelToken? cancelToken}) async => [];

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
    final result = fakeAnalysis ??
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
    final index = fakeRequests.indexWhere((request) => request.serviceRequestId == id);
    if (index >= 0) {
      fakeRequests[index] = fakeRequests[index].copyWith(
        status: result.status, category: result.category, urgency: result.urgency,
      );
    }
    return result;
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

  bool shouldSubmitClarificationThrow = false;
  int submitClarificationCallCount = 0;
  SubmitClarificationAnswersDto? lastSubmittedAnswersDto;

  @override
  Future<List<ServiceRequestClarificationModel>> submitClarificationAnswers(
    String id,
    SubmitClarificationAnswersDto dto,
  ) async {
    submitClarificationCallCount++;
    lastSubmittedAnswersDto = dto;
    if (shouldThrow || shouldSubmitClarificationThrow) {
      throw Exception('Submit clarification answers failed');
    }
    final index = fakeRequests.indexWhere((r) => r.serviceRequestId == id);
    if (index < 0) throw Exception('Request not found');
    final req = fakeRequests[index];
    final updatedList = req.clarifications.map((c) {
      final item = dto.answers.cast<ClarificationAnswerSubmissionItemDto?>().firstWhere(
        (a) => a?.clarificationId == c.id,
        orElse: () => null,
      );
      if (item != null) {
        return c.copyWith(answer: item.answer, answeredAt: DateTime.now());
      }
      return c;
    }).toList();
    fakeRequests[index] = req.copyWith(clarifications: updatedList);
    return updatedList;
  }

  @override
  String getErrorMessage(Object error) {
    return 'Mock error: ${error.toString()}';
  }
}

/**
 * TEST SUITE: ServiceRequestProvider Tests
 *
 * 1. What this test suite tests:
 *    - State management, asynchronous orchestration, lifecycle transitions, and reconciliation polling in `ServiceRequestProvider`.
 *    - Verifies CRUD actions (load, create, update, cancel), AI analysis execution (`analyzeRequest`), idempotent debouncing/in-flight guards (`isAnalyzing`),
 *      timeout/409 error handling with background synchronization, polling limits, and structured clarification answer submission (`submitClarificationAnswers`, `submitClarificationAnswersAndReanalyze`).
 * 2. Why these tests are needed:
 *    - `ServiceRequestProvider` is the central ChangeNotifier/state store driving customer service request screens in the Flutter app.
 *      Since AI problem understanding can take several seconds and encounter network timeouts or concurrent requests (HTTP 409), robust handling of
 *      race conditions, reconciliation polling, and safe failure fallback is essential for a smooth mobile user experience without UI lockups or duplicate AI jobs.
 * 3. What is real and what is mocked:
 *    - Real `ServiceRequestProvider` ChangeNotifier instance.
 *    - Mocked/Faked: `FakeServiceRequestService` fakes HTTP API calls, simulating in-memory request storage, customizable delays via `Completer`,
 *      network exceptions (`DioException`), status transitions, and call count tracking.
 * 4. Prerequisites: None.
 * 5. PowerShell command to run this suite from repository root:
 *    Push-Location mobile; flutter test test/service_request_provider_test.dart; Pop-Location
 */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that `loadMyRequests` fetches customer requests from the service, updates `provider.requests`, and resets loading/error states.
   *
   * Why: Why this scenario matters
   * - Ensures initial screen load populates the list of customer service requests correctly.
   *
   * Scenario: Input or condition being tested
   * - Call `loadMyRequests` with fake service returning 1 request.
   *
   * Pass condition: What result or assertion makes it pass
   * - `provider.requests` has length 1, contains `'req-1'`, `isLoading` is false, `error` is null.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/service_request_provider_test.dart --plain-name "loadMyRequests updates requests list"; Pop-Location
   */
  test('loadMyRequests updates requests list', () async {
    final success = await provider.loadMyRequests();

    expect(success, true);
    expect(provider.requests.length, 1);
    expect(provider.requests.first.serviceRequestId, 'req-1');
    expect(provider.isLoading, false);
    expect(provider.error, isNull);
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that `createRequest` inserts a newly created request at the top of the requests list and sets `currentRequest`.
   *
   * Why: Why this scenario matters
   * - Allows immediate navigation to the detail screen of the newly created service request.
   *
   * Scenario: Input or condition being tested
   * - Submit valid `CreateServiceRequestDto`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Returned request is non-null, list count increases to 2, `currentRequest.serviceRequestId == 'req-new'`.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/service_request_provider_test.dart --plain-name "createRequest adds new request to list and sets currentRequest"; Pop-Location
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that `cancelRequest` transitions request status to `Cancelled` and updates both the returned model and the cached list.
   *
   * Why: Why this scenario matters
   * - Reflects immediate cancellation in the UI without requiring a full manual refresh.
   *
   * Scenario: Input or condition being tested
   * - Cancel an existing request `'req-1'`.
   *
   * Pass condition: What result or assertion makes it pass
   * - `cancelled.status == ServiceRequestStatus.cancelled` and cached request status matches.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/service_request_provider_test.dart --plain-name "cancelRequest updates status to cancelled"; Pop-Location
   */
  test('cancelRequest updates status to cancelled', () async {
    await provider.loadMyRequests();
    final cancelled = await provider.cancelRequest('req-1');

    expect(cancelled, isNotNull);
    expect(cancelled?.status, ServiceRequestStatus.cancelled);
    expect(provider.requests.first.status, ServiceRequestStatus.cancelled);
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies successful problem understanding analysis, setting `currentAnalysis` and updating `currentRequest` status and urgency.
   *
   * Why: Why this scenario matters
   * - AI diagnostic result must be presented directly to the user upon completion.
   *
   * Scenario: Input or condition being tested
   * - Call `analyzeRequest` on an existing request where the service completes successfully.
   *
   * Pass condition: What result or assertion makes it pass
   * - `currentAnalysis` has problem summary, `currentRequest.status == analyzed`, `urgency == high`.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/service_request_provider_test.dart --plain-name "analyzeRequest sets currentAnalysis and updates request status"; Pop-Location
   */
  test('analyzeRequest sets currentAnalysis and updates request status', () async {
    await provider.loadRequestById('req-1');
    final analysis = await provider.analyzeRequest('req-1');

    expect(analysis, isNotNull);
    expect(provider.currentAnalysis?.problemSummary, 'Pipe leakage in bathroom');
    expect(provider.currentRequest?.status, ServiceRequestStatus.analyzed);
    expect(provider.currentRequest?.urgency, ServiceRequestUrgency.high);
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies transition of request to `ReadyForMatching` status.
   *
   * Why: Why this scenario matters
   * - When problem understanding completes, the customer confirms requirements to enter the matching queue.
   *
   * Scenario: Input or condition being tested
   * - Call `markReadyForMatching` on an analyzed request.
   *
   * Pass condition: What result or assertion makes it pass
   * - Request status updates to `readyForMatching` in both return value and `currentRequest`.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/service_request_provider_test.dart --plain-name "markReadyForMatching updates status to readyForMatching"; Pop-Location
   */
  test('markReadyForMatching updates status to readyForMatching', () async {
    await provider.loadRequestById('req-1');
    final updated = await provider.markReadyForMatching('req-1');

    expect(updated, isNotNull);
    expect(updated?.status, ServiceRequestStatus.readyForMatching);
    expect(provider.currentRequest?.status, ServiceRequestStatus.readyForMatching);
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies error handling when service throws during `loadMyRequests`.
   *
   * Why: Why this scenario matters
   * - Network failures must not crash the app; they should set a readable error message and reset loading flag.
   *
   * Scenario: Input or condition being tested
   * - Fake service configured with `shouldThrow = true`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Returns false, `provider.error` is not null, `isLoading` is false.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/service_request_provider_test.dart --plain-name "handles service errors gracefully"; Pop-Location
   */
  test('handles service errors gracefully', () async {
    fakeService.shouldThrow = true;

    final success = await provider.loadMyRequests();

    expect(success, false);
    expect(provider.error, isNotNull);
    expect(provider.isLoading, false);
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that in-flight analysis prevents starting a concurrent analysis request (in-flight guard / debouncing).
   *
   * Why: Why this scenario matters
   * - Prevents double-submitting expensive AI workflows if user double-taps "Analyze".
   *
   * Scenario: Input or condition being tested
   * - Fire `analyzeRequest` while an existing analysis is pending via Completer.
   *
   * Pass condition: What result or assertion makes it pass
   * - Second call returns null, `fakeService.analyzeCallCount == 1`, first call completes successfully.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/service_request_provider_test.dart --plain-name "analyzeRequest_WhenAlreadyAnalyzing_DoesNotStartSecondRequest"; Pop-Location
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies concurrency protection under immediate simultaneous invocations (`Future.wait`).
   *
   * Why: Why this scenario matters
   * - Protects backend from simultaneous tap events before state flags propagate.
   *
   * Scenario: Input or condition being tested
   * - Invoke `provider.analyzeRequest('req-1')` twice synchronously.
   *
   * Pass condition: What result or assertion makes it pass
   * - `fakeService.analyzeCallCount == 1`, first future succeeds, second future returns null.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/service_request_provider_test.dart --plain-name "Rapid double invocation results in only one service.analyze call"; Pop-Location
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that on HTTP receive timeout during analysis, the provider triggers a GET reconciliation to fetch the latest request state.
   *
   * Why: Why this scenario matters
   * - The backend analysis may still be processing asynchronously or have completed despite the client timeout; refreshing prevents out-of-sync state.
   *
   * Scenario: Input or condition being tested
   * - Service throws `DioExceptionType.receiveTimeout`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Returns null, `getByIdCallCount` increments, `isAnalyzing` resets to false, error remains null if status refreshed safely.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/service_request_provider_test.dart --plain-name "analyzeRequest_WhenTimeoutOccurs_RefreshesRequestFromBackend"; Pop-Location
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that an HTTP 409 Conflict (analysis already running or state changed) triggers a GET refresh and sets an informative message.
   *
   * Why: Why this scenario matters
   * - Informs the customer that the request is already being processed without treating it as an unrecoverable failure.
   *
   * Scenario: Input or condition being tested
   * - Service throws 409 response.
   *
   * Pass condition: What result or assertion makes it pass
   * - `getByIdCallCount` increments, `provider.error` contains informative conflict/refresh message, `isAnalyzing` resets to false.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/service_request_provider_test.dart --plain-name "analyzeRequest_When409Occurs_RefreshesRequestFromBackend"; Pop-Location
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that when a timeout occurs and the backend is found in `Analyzing` state, `currentRequest` is updated to `analyzing`.
   *
   * Why: Why this scenario matters
   * - Shows the user that analysis is underway in the cloud even if the initial synchronous HTTP call timed out.
   *
   * Scenario: Input or condition being tested
   * - Timeout occurs, subsequent GET returns `status: ServiceRequestStatus.analyzing`.
   *
   * Pass condition: What result or assertion makes it pass
   * - `provider.currentRequest?.status == ServiceRequestStatus.analyzing`, `isAnalyzing` is false.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/service_request_provider_test.dart --plain-name "Refreshed backend status Analyzing updates currentRequest"; Pop-Location
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that `isAnalyzing` boolean always resets to `false` across every exit branch (success, timeout, conflict, synchronization crash).
   *
   * Why: Why this scenario matters
   * - Prevents the UI from remaining stuck in an unclickable loading spinner forever.
   *
   * Scenario: Input or condition being tested
   * - Sequentially trigger 4 scenarios: success, timeout, 409, and timeout with sync failure.
   *
   * Pass condition: What result or assertion makes it pass
   * - `provider.isAnalyzing` is false after each operation.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/service_request_provider_test.dart --plain-name "isAnalyzing always resets safely after success, timeout, 409, and failed synchronization"; Pop-Location
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies user-friendly error string formatting for generic CRUD network errors (timeout, 409 conflict).
   *
   * Why: Why this scenario matters
   * - Raw Dio exceptions must never be shown to end users.
   *
   * Scenario: Input or condition being tested
   * - Format `DioExceptionType.receiveTimeout` and HTTP 409 error.
   *
   * Pass condition: What result or assertion makes it pass
   * - Returns 'Request timed out. Please try again.' and 'The request could not be completed due to a conflict.'
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/service_request_provider_test.dart --plain-name "serviceRequestService getErrorMessage formats generic CRUD errors properly"; Pop-Location
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies specific analysis error message generation for timeout and 409 conflict scenarios.
   *
   * Why: Why this scenario matters
   * - Analysis-specific errors require distinct guidance about background processing and automatic refresh.
   *
   * Scenario: Input or condition being tested
   * - Pass timeout and 409 exceptions to `getAnalysisErrorMessage`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Messages mention taking longer than expected / currently being analyzed and that status has been refreshed.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/service_request_provider_test.dart --plain-name "serviceRequestService getAnalysisErrorMessage formats timeout and conflict errors properly"; Pop-Location
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that if analysis fails AND subsequent status reconciliation also fails, `analysisStateNeedsRefresh` flag is set to true to block further analysis.
   *
   * Why: Why this scenario matters
   * - Protects against repeated hammering of a failing backend when state cannot be verified.
   *
   * Scenario: Input or condition being tested
   * - Analysis times out and `getById` also throws.
   *
   * Pass condition: What result or assertion makes it pass
   * - `provider.analysisStateNeedsRefresh == true`, subsequent `analyzeRequest` calls are blocked without calling service.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/service_request_provider_test.dart --plain-name "analysis fails and synchronization fails sets analysisStateNeedsRefresh to true"; Pop-Location
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that calling `loadRequestById` successfully clears the `analysisStateNeedsRefresh` latch.
   *
   * Why: Why this scenario matters
   * - Once network connectivity recovers and status is refreshed, normal actions can resume.
   *
   * Scenario: Input or condition being tested
   * - Transition from failed sync state to successful `loadRequestById`.
   *
   * Pass condition: What result or assertion makes it pass
   * - `refreshed` is non-null, `analysisStateNeedsRefresh == false`, `currentRequest.status` updated.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/service_request_provider_test.dart --plain-name "subsequent successful refresh clears analysisStateNeedsRefresh and updates currentRequest"; Pop-Location
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that when timeout occurs and immediate GET returns `Analyzed`, the provider synchronizes request state without fabricating a fake `currentAnalysis` object.
   *
   * Why: Why this scenario matters
   * - Discrepancy / architectural guarantee: Provider must not synthesize artificial analysis results; it relies strictly on authoritative backend data.
   *
   * Scenario: Input or condition being tested
   * - Timeout on POST /analyze, subsequent GET returns status `analyzed`.
   *
   * Pass condition: What result or assertion makes it pass
   * - `currentRequest.status == analyzed`, `currentAnalysis` is null, `analysisStateNeedsRefresh` is false.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/service_request_provider_test.dart --plain-name "Analyze timeout + immediate GET returns Analyzed synchronizes final state without fake analysis"; Pop-Location
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies reconciliation when the backend analysis determines more information is needed (`awaitingInformation`).
   *
   * Why: Why this scenario matters
   * - Customer must be shown clarification questions when AI needs more details.
   *
   * Scenario: Input or condition being tested
   * - Timeout on analyze, immediate GET returns `awaitingInformation`.
   *
   * Pass condition: What result or assertion makes it pass
   * - `currentRequest.status == awaitingInformation`, `error` is null, `analysisStateNeedsRefresh` is false.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/service_request_provider_test.dart --plain-name "Analyze timeout + immediate GET returns AwaitingInformation synchronizes final state"; Pop-Location
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies reconciliation polling loop: POST times out, first GET returns `Analyzing`, second GET poll returns `Analyzed`.
   *
   * Why: Why this scenario matters
   * - Verifies that reconciliation uses GET requests only and never re-issues duplicate POST /analyze calls.
   *
   * Scenario: Input or condition being tested
   * - Sequential GET responses: `analyzing`, then `analyzed`.
   *
   * Pass condition: What result or assertion makes it pass
   * - `currentRequest.status == analyzed`, `currentRequest.category == 'Plumbing'`, `fakeService.analyzeCallCount == 1`.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/service_request_provider_test.dart --plain-name "Analyze timeout + first GET Analyzing + 2nd poll Analyzed succeeds with GET only"; Pop-Location
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies reconciliation polling transitioning from `Analyzing` to `AwaitingInformation`.
   *
   * Why: Why this scenario matters
   * - Ensures multi-poll convergence to clarification state.
   *
   * Scenario: Input or condition being tested
   * - First GET returns `analyzing`, second GET returns `awaitingInformation`.
   *
   * Pass condition: What result or assertion makes it pass
   * - `currentRequest.status == awaitingInformation`, `error` is null, `analyzeCallCount == 1`.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/service_request_provider_test.dart --plain-name "Analyze timeout + first GET Analyzing + 2nd poll AwaitingInformation succeeds"; Pop-Location
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies recovery when backend resets request status back to `Created` after an analysis failure.
   *
   * Why: Why this scenario matters
   * - If the backend agent times out or crashes and rolls back to `Created`, the client must reflect this state so user can retry.
   *
   * Scenario: Input or condition being tested
   * - First GET returns `analyzing`, second GET returns `created`.
   *
   * Pass condition: What result or assertion makes it pass
   * - `currentRequest.status == created`, `error` is null, `isAnalyzing` is false.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/service_request_provider_test.dart --plain-name "Analyze timeout + first GET Analyzing + 2nd poll Created (backend recovery) succeeds"; Pop-Location
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that reconciliation polling terminates immediately once status is no longer `Analyzing`, rather than continuing until `maxPolls`.
   *
   * Why: Why this scenario matters
   * - Conserves mobile network bandwidth and battery life.
   *
   * Scenario: Input or condition being tested
   * - `maxPolls: 10`, but status changes to `analyzed` on the 1st poll.
   *
   * Pass condition: What result or assertion makes it pass
   * - Exactly 2 GET calls are made (initial GET + 1 poll), stopping well before the 10-poll limit.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/service_request_provider_test.dart --plain-name "Polling stops immediately when status != Analyzing without exhausting limit"; Pop-Location
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that polling respects the upper bound `maxPolls` and terminates cleanly when backend remains in `Analyzing` state.
   *
   * Why: Why this scenario matters
   * - Prevents infinite polling loops in mobile clients if the backend agent hangs indefinitely.
   *
   * Scenario: Input or condition being tested
   * - Backend remains in `analyzing` across `maxPolls: 4`.
   *
   * Pass condition: What result or assertion makes it pass
   * - Exactly 5 GET calls made (initial GET + 4 polls), `isAnalyzing` resets to false, status is `analyzing`.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/service_request_provider_test.dart --plain-name "Polling is bounded and cannot run forever (grace-period expiry)"; Pop-Location
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that network failure during polling loop marks `analysisStateNeedsRefresh == true`.
   *
   * Why: Why this scenario matters
   * - Flags to the UI that current in-memory status may be stale and requires manual pull-to-refresh.
   *
   * Scenario: Input or condition being tested
   * - Initial GET succeeds with `analyzing`, next poll throws network error.
   *
   * Pass condition: What result or assertion makes it pass
   * - `provider.analysisStateNeedsRefresh == true`, `provider.error` is not null, `isAnalyzing` is false.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/service_request_provider_test.dart --plain-name "Reconciliation GET network failure during polling sets analysisStateNeedsRefresh"; Pop-Location
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that customer's optional category hint is preserved during status reconciliation polling.
   *
   * Why: Why this scenario matters
   * - Customer input must never be discarded or corrupted during background synchronization.
   *
   * Scenario: Input or condition being tested
   * - Request with `categoryHint: 'Vehicle Repair'` undergoes timeout and polling reconciliation.
   *
   * Pass condition: What result or assertion makes it pass
   * - `currentRequest.categoryHint == 'Vehicle Repair'` and `currentRequest.category == 'Plumbing'`.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/service_request_provider_test.dart --plain-name "CategoryHint survives reconciliation and refreshes intact"; Pop-Location
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that client errors (HTTP 400 Bad Request) execute only 1 synchronization GET and do not initiate polling.
   *
   * Why: Why this scenario matters
   * - Deterministic validation failures cannot resolve spontaneously via polling; polling would waste resources.
   *
   * Scenario: Input or condition being tested
   * - Service returns 400 error.
   *
   * Pass condition: What result or assertion makes it pass
   * - Exactly 1 GET call executed, `error` is set, `isAnalyzing` is false.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/service_request_provider_test.dart --plain-name "Deterministic HTTP 400 does not enter reconciliation polling"; Pop-Location
   */
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

  /**
   * Purpose: What this specific test checks
   * - Verifies that when POST /analyze succeeds but subsequent detail GET fails, subsequent `analyzeRequest` calls are blocked from re-posting.
   *
   * Why: Why this scenario matters
   * - Prevents re-executing analysis after it already succeeded on the backend.
   *
   * Scenario: Input or condition being tested
   * - Analysis completes, detail GET throws, followed by another `analyzeRequest` call.
   *
   * Pass condition: What result or assertion makes it pass
   * - `analyzeCallCount` remains 1, `analysisStateNeedsRefresh` is true.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/service_request_provider_test.dart --plain-name "successful analysis with failed detail GET never repeats the analysis POST"; Pop-Location
   */
  test('successful analysis with failed detail GET never repeats the analysis POST', () async {
    fakeService.fakeRequests = [sampleRequest];
    await provider.loadRequestById('req-1');
    fakeService.shouldGetByIdThrow = true;
    final result = await provider.analyzeRequest('req-1');
    expect(result, isNotNull);
    expect(fakeService.analyzeCallCount, 1);
    expect(provider.analysisStateNeedsRefresh, isTrue);
    expect(provider.isAnalyzing, isFalse);
    await provider.analyzeRequest('req-1');
    expect(fakeService.analyzeCallCount, 1);
  });

  /**
   * TEST GROUP: Clarification Answers Submission
   *
   * 1. What this group tests:
   *    - Verifies submitting customer answers to clarification questions (`submitClarificationAnswers`) and chained submission with automatic re-analysis (`submitClarificationAnswersAndReanalyze`).
   *    - Confirms updating in-memory clarification models, error handling on service failure, and aborting re-analysis if submission fails.
   * 2. Why these tests are needed:
   *    - Component 1 problem understanding relies on interactive clarification dialogs.
   *      Ensuring answers are reliably recorded and re-analysis triggered correctly without race conditions is critical.
   * 3. What is real and what is mocked:
   *    - Real `ServiceRequestProvider` ChangeNotifier instance.
   *    - Mocked: `FakeServiceRequestService` tracking submission payloads and call counts.
   * 4. Prerequisites: None.
   * 5. PowerShell command to run this group from repository root:
   *    Push-Location mobile; flutter test test/service_request_provider_test.dart --plain-name "Clarification Answers Submission"; Pop-Location
   */
  group('Clarification Answers Submission', () {
    /**
     * Purpose: What this specific test checks
     * - Verifies that `submitClarificationAnswers` persists answers into the in-memory `currentRequest` and flags `currentRoundIsFullyAnswered`.
     *
     * Why: Why this scenario matters
     * - The mobile UI immediately updates to show answered state.
     *
     * Scenario: Input or condition being tested
     * - Submit answer `'Master bedroom'` for question `'c-1'`.
     *
     * Pass condition: What result or assertion makes it pass
     * - Returns true, `clarifications.first.answer == 'Master bedroom'`, `currentRoundIsFullyAnswered == true`.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_provider_test.dart --plain-name "Clarification Answers Submission submitClarificationAnswers updates currentRequest clarifications and returns true"; Pop-Location
     */
    test('submitClarificationAnswers updates currentRequest clarifications and returns true', () async {
      const clarification = ServiceRequestClarificationModel(
        id: 'c-1',
        clarificationRound: 1,
        sequence: 1,
        question: 'Which room?',
      );
      final reqWithClarification = sampleRequest.copyWith(
        status: ServiceRequestStatus.awaitingInformation,
        clarifications: [clarification],
      );
      fakeService.fakeRequests = [reqWithClarification];
      await provider.loadRequestById('req-1');

      final result = await provider.submitClarificationAnswers(
        'req-1',
        1,
        {'c-1': 'Master bedroom'},
      );

      expect(result, true);
      expect(provider.error, isNull);
      expect(fakeService.submitClarificationCallCount, 1);
      expect(fakeService.lastSubmittedAnswersDto?.clarificationRound, 1);
      expect(fakeService.lastSubmittedAnswersDto?.answers.first.answer, 'Master bedroom');
      expect(provider.currentRequest?.clarifications.first.answer, 'Master bedroom');
      expect(provider.currentRequest?.currentRoundIsFullyAnswered, true);
    });

    /**
     * Purpose: What this specific test checks
     * - Verifies error handling when answer submission fails.
     *
     * Why: Why this scenario matters
     * - Informs the customer that answers failed to save so they can retry.
     *
     * Scenario: Input or condition being tested
     * - Fake service configured with `shouldSubmitClarificationThrow = true`.
     *
     * Pass condition: What result or assertion makes it pass
     * - Returns false, `provider.error` is not null.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_provider_test.dart --plain-name "Clarification Answers Submission submitClarificationAnswers sets error and returns false when service throws"; Pop-Location
     */
    test('submitClarificationAnswers sets error and returns false when service throws', () async {
      fakeService.shouldSubmitClarificationThrow = true;
      fakeService.fakeRequests = [sampleRequest];
      await provider.loadRequestById('req-1');

      final result = await provider.submitClarificationAnswers(
        'req-1',
        1,
        {'c-1': 'Master bedroom'},
      );

      expect(result, false);
      expect(provider.error, isNotNull);
    });

    /**
     * Purpose: What this specific test checks
     * - Verifies chained workflow: successfully submitting answers immediately triggers AI re-analysis.
     *
     * Why: Why this scenario matters
     * - Streamlines user experience so the user doesn't have to tap two separate buttons ("Save Answers" then "Analyze").
     *
     * Scenario: Input or condition being tested
     * - Call `submitClarificationAnswersAndReanalyze` on valid request.
     *
     * Pass condition: What result or assertion makes it pass
     * - Both `submitClarificationCallCount` and `analyzeCallCount` equal 1, returned analysis is non-null, `status == analyzed`.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_provider_test.dart --plain-name "Clarification Answers Submission submitClarificationAnswersAndReanalyze submits answers and triggers re-analysis"; Pop-Location
     */
    test('submitClarificationAnswersAndReanalyze submits answers and triggers re-analysis', () async {
      const clarification = ServiceRequestClarificationModel(
        id: 'c-1',
        clarificationRound: 1,
        sequence: 1,
        question: 'Which room?',
      );
      final reqWithClarification = sampleRequest.copyWith(
        status: ServiceRequestStatus.awaitingInformation,
        clarifications: [clarification],
      );
      fakeService.fakeRequests = [reqWithClarification];
      await provider.loadRequestById('req-1');

      final analysis = await provider.submitClarificationAnswersAndReanalyze(
        'req-1',
        1,
        {'c-1': 'Master bedroom'},
      );

      expect(fakeService.submitClarificationCallCount, 1);
      expect(fakeService.analyzeCallCount, 1);
      expect(analysis, isNotNull);
      expect(provider.currentRequest?.clarifications.first.answer, 'Master bedroom');
      expect(provider.currentRequest?.status, ServiceRequestStatus.analyzed);
    });

    /**
     * Purpose: What this specific test checks
     * - Verifies that if answer submission fails, re-analysis is aborted immediately.
     *
     * Why: Why this scenario matters
     * - Analysis should never be triggered on incomplete or unpersisted answers.
     *
     * Scenario: Input or condition being tested
     * - Service throws during clarification answer submission.
     *
     * Pass condition: What result or assertion makes it pass
     * - Returns null, `submitClarificationCallCount == 1`, `analyzeCallCount == 0`, `provider.error` is set.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_provider_test.dart --plain-name "Clarification Answers Submission submitClarificationAnswersAndReanalyze aborts and does not call analyze if answer submission fails"; Pop-Location
     */
    test('submitClarificationAnswersAndReanalyze aborts and does not call analyze if answer submission fails', () async {
      fakeService.shouldSubmitClarificationThrow = true;
      fakeService.fakeRequests = [sampleRequest];
      await provider.loadRequestById('req-1');

      final analysis = await provider.submitClarificationAnswersAndReanalyze(
        'req-1',
        1,
        {'c-1': 'Master bedroom'},
      );

      expect(analysis, isNull);
      expect(fakeService.submitClarificationCallCount, 1);
      expect(fakeService.analyzeCallCount, 0);
      expect(provider.error, isNotNull);
    });
  });
}
