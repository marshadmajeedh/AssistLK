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

  @override
  Future<List<ServiceRequestModel>> getMyRequests() async {
    if (shouldThrow) throw Exception('Network error');
    return List.of(fakeRequests);
  }

  @override
  Future<ServiceRequestModel> getById(String id) async {
    if (shouldThrow) throw Exception('Request not found');
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
    if (shouldThrow) throw Exception('Analysis failed');
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
    provider = ServiceRequestProvider(serviceRequestService: fakeService);
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
}
