import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/api/api_client.dart';
import 'package:mobile/core/auth/token_storage.dart';
import 'package:mobile/features/auth/models/auth_user.dart';
import 'package:mobile/features/auth/providers/auth_provider.dart';
import 'package:mobile/features/auth/services/auth_service.dart';
import 'package:mobile/features/service_requests/models/create_service_request_dto.dart';
import 'package:mobile/features/service_requests/models/problem_understanding_result_model.dart';
import 'package:mobile/features/service_requests/models/service_request_model.dart';
import 'package:mobile/features/service_requests/models/service_request_status.dart';
import 'package:mobile/features/service_requests/models/service_request_urgency.dart';
import 'package:mobile/features/service_requests/models/update_service_request_dto.dart';
import 'package:mobile/features/service_requests/providers/service_request_provider.dart';
import 'package:mobile/features/service_requests/screens/create_service_request_screen.dart';
import 'package:mobile/features/service_requests/screens/customer_home_screen.dart';
import 'package:mobile/features/service_requests/screens/edit_service_request_screen.dart';
import 'package:mobile/features/service_requests/screens/service_request_detail_screen.dart';
import 'package:mobile/features/service_requests/services/service_request_service.dart';
import 'package:mobile/features/service_requests/widgets/analysis_result_card.dart';
import 'package:mobile/features/service_requests/widgets/clarification_section.dart';
import 'package:mobile/features/service_requests/widgets/ready_for_matching_section.dart';
import 'package:provider/provider.dart';

class MockServiceRequestService extends ServiceRequestService {
  MockServiceRequestService() : super(apiClient: ApiClient());

  List<ServiceRequestModel> mockRequests = [];
  ProblemUnderstandingResultModel? mockAnalysis;

  @override
  Future<List<ServiceRequestModel>> getMyRequests() async => List.of(mockRequests);

  @override
  Future<ServiceRequestModel> getById(String id) async =>
      mockRequests.firstWhere((r) => r.serviceRequestId == id);

  @override
  Future<ServiceRequestModel> create(CreateServiceRequestDto dto) async {
    final created = ServiceRequestModel(
      serviceRequestId: 'req-new',
      customerId: 'cust-1',
      category: 'General',
      description: dto.description,
      locationText: dto.locationText,
      urgency: ServiceRequestUrgency.low,
      status: ServiceRequestStatus.created,
      createdAt: DateTime(2026, 9, 9),
      updatedAt: DateTime(2026, 9, 9),
    );
    mockRequests.insert(0, created);
    return created;
  }

  @override
  Future<ServiceRequestModel> update(String id, UpdateServiceRequestDto dto) async {
    final idx = mockRequests.indexWhere((r) => r.serviceRequestId == id);
    final updated = mockRequests[idx].copyWith(
      description: dto.description,
      locationText: dto.locationText,
    );
    mockRequests[idx] = updated;
    return updated;
  }

  @override
  Future<ProblemUnderstandingResultModel> analyze(String id) async {
    return mockAnalysis ??
        ProblemUnderstandingResultModel(
          workflowId: 'wf-1',
          executionId: 'ex-1',
          serviceRequestId: id,
          status: ServiceRequestStatus.analyzed,
          category: 'Electrical Wiring',
          problemSummary: 'Short circuit detected in main DB',
          urgency: ServiceRequestUrgency.high,
          confidence: 0.95,
          needsMoreInformation: false,
          followUpQuestions: const [],
        );
  }

  @override
  Future<ServiceRequestModel> markReadyForMatching(String id) async {
    final idx = mockRequests.indexWhere((r) => r.serviceRequestId == id);
    final updated = mockRequests[idx].copyWith(
      status: ServiceRequestStatus.readyForMatching,
    );
    mockRequests[idx] = updated;
    return updated;
  }
}

class FakeAuthProvider extends AuthProvider {
  FakeAuthProvider()
      : super(
          authService: AuthService(apiClient: ApiClient()),
          tokenStorage: TokenStorage(),
        );

  @override
  AuthUser? get user => const AuthUser(
        userId: 'cust-1',
        fullName: 'Kamal Perera',
        email: 'kamal@assistlk.com',
        role: 'Customer',
      );
}

void main() {
  late MockServiceRequestService mockService;
  late ServiceRequestProvider requestProvider;
  late FakeAuthProvider authProvider;

  Widget buildApp(Widget child) {
    return MultiProvider(
      providers: [
        ChangeNotifierProvider<AuthProvider>.value(value: authProvider),
        ChangeNotifierProvider<ServiceRequestProvider>.value(
          value: requestProvider,
        ),
      ],
      child: MaterialApp(
        home: child,
      ),
    );
  }

  setUp(() {
    mockService = MockServiceRequestService();
    requestProvider = ServiceRequestProvider(serviceRequestService: mockService);
    authProvider = FakeAuthProvider();
  });

  group('CustomerHomeScreen', () {
    testWidgets('displays welcome banner and empty state when no requests exist',
        (tester) async {
      await tester.pumpWidget(buildApp(const CustomerHomeScreen()));
      await tester.pumpAndSettle();

      expect(find.text('Welcome, Kamal Perera'), findsOneWidget);
      expect(find.text('kamal@assistlk.com'), findsOneWidget);
      expect(find.text('No Service Requests Yet'), findsOneWidget);
      expect(find.text('Create Request'), findsNWidgets(2)); // header button + empty state button
    });

    testWidgets('displays service request list when requests exist',
        (tester) async {
      mockService.mockRequests = [
        ServiceRequestModel(
          serviceRequestId: 'req-1',
          customerId: 'cust-1',
          category: 'Plumbing',
          description: 'Bathroom tap is dripping continuously',
          locationText: 'Colombo 03',
          urgency: ServiceRequestUrgency.low,
          status: ServiceRequestStatus.created,
          createdAt: DateTime(2026, 9, 9),
          updatedAt: DateTime(2026, 9, 9),
        ),
      ];

      await tester.pumpWidget(buildApp(const CustomerHomeScreen()));
      await tester.pumpAndSettle();

      expect(find.text('Plumbing'), findsOneWidget);
      expect(find.text('Bathroom tap is dripping continuously'), findsOneWidget);
      expect(find.text('Created'), findsOneWidget);
      expect(find.text('Low'), findsOneWidget);
    });
  });

  group('CreateServiceRequestScreen', () {
    testWidgets('validates required inputs and submits', (tester) async {
      await tester.pumpWidget(buildApp(const CreateServiceRequestScreen()));
      await tester.pumpAndSettle();

      expect(find.text('Create Service Request'), findsOneWidget);
      expect(find.text('Submit Request'), findsOneWidget);

      // Tap submit without typing -> should show validation errors
      await tester.tap(find.text('Submit Request'));
      await tester.pumpAndSettle();

      expect(find.text('Please describe the problem you are experiencing.'), findsOneWidget);
      expect(find.text('Please provide the service location.'), findsOneWidget);

      // Enter valid text
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Problem Description'),
        'Major ceiling leak in the living room after heavy rain',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Location / Address'),
        '123 Galle Road, Colombo 03',
      );

      await tester.tap(find.text('Submit Request'));
      await tester.pumpAndSettle();

      expect(mockService.mockRequests.length, 1);
      expect(mockService.mockRequests.first.description,
          'Major ceiling leak in the living room after heavy rain');
    });
  });

  group('ServiceRequestDetailScreen Status-Driven Logic', () {
    testWidgets('Created status shows Analyze with Gemini AI button', (tester) async {
      final sample = ServiceRequestModel(
        serviceRequestId: 'req-c',
        customerId: 'cust-1',
        category: 'Electrical',
        description: 'Breaker keeps tripping',
        locationText: 'Colombo',
        urgency: ServiceRequestUrgency.medium,
        status: ServiceRequestStatus.created,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.mockRequests = [sample];

      await tester.pumpWidget(
        buildApp(const ServiceRequestDetailScreen(requestId: 'req-c')),
      );
      await tester.pumpAndSettle();

      expect(find.text('Request Details'), findsOneWidget);
      expect(find.text('Breaker keeps tripping'), findsOneWidget);
      expect(find.text('Analyze with Gemini AI'), findsOneWidget);

      // Trigger analysis
      await tester.tap(find.text('Analyze with Gemini AI'));
      await tester.pumpAndSettle();

      expect(find.byType(AnalysisResultCard), findsOneWidget);
    });

    testWidgets('Analyzed status shows AnalysisResultCard and Mark Ready button',
        (tester) async {
      final sample = ServiceRequestModel(
        serviceRequestId: 'req-a',
        customerId: 'cust-1',
        category: 'Carpentry',
        description: 'Door frame is broken',
        locationText: 'Kandy',
        urgency: ServiceRequestUrgency.medium,
        status: ServiceRequestStatus.analyzed,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.mockRequests = [sample];

      await tester.pumpWidget(
        buildApp(const ServiceRequestDetailScreen(requestId: 'req-a')),
      );
      await tester.pumpAndSettle();

      expect(find.byType(AnalysisResultCard), findsOneWidget);
      expect(find.text('Mark Ready for Matching'), findsOneWidget);

      await tester.ensureVisible(find.text('Mark Ready for Matching'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Mark Ready for Matching'));
      await tester.pumpAndSettle();

      expect(find.byType(ReadyForMatchingSection), findsOneWidget);
    });

    testWidgets('AwaitingInformation status shows ClarificationSection',
        (tester) async {
      final sample = ServiceRequestModel(
        serviceRequestId: 'req-ai',
        customerId: 'cust-1',
        category: 'Plumbing',
        description: 'Water pressure is low',
        locationText: 'Colombo',
        urgency: ServiceRequestUrgency.low,
        status: ServiceRequestStatus.awaitingInformation,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.mockRequests = [sample];

      await tester.pumpWidget(
        buildApp(const ServiceRequestDetailScreen(requestId: 'req-ai')),
      );
      await tester.pumpAndSettle();

      expect(find.byType(ClarificationSection), findsOneWidget);
      expect(find.text('Clarification Needed'), findsOneWidget);
      expect(find.text('Edit Details'), findsOneWidget);
      expect(find.text('Re-analyze'), findsOneWidget);
    });

    testWidgets('ReadyForMatching status shows ReadyForMatchingSection',
        (tester) async {
      final sample = ServiceRequestModel(
        serviceRequestId: 'req-rfm',
        customerId: 'cust-1',
        category: 'Roofing',
        description: 'Tiles damaged on roof',
        locationText: 'Galle',
        urgency: ServiceRequestUrgency.high,
        status: ServiceRequestStatus.readyForMatching,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.mockRequests = [sample];

      await tester.pumpWidget(
        buildApp(const ServiceRequestDetailScreen(requestId: 'req-rfm')),
      );
      await tester.pumpAndSettle();

      expect(find.byType(ReadyForMatchingSection), findsOneWidget);
      expect(find.text('Request Understood'), findsOneWidget);
      expect(find.text('Ready for provider matching'), findsOneWidget);
    });
  });

  group('EditServiceRequestScreen', () {
    testWidgets('pre-fills existing request details and saves update', (tester) async {
      final sample = ServiceRequestModel(
        serviceRequestId: 'req-edit',
        customerId: 'cust-1',
        category: 'General',
        description: 'Original description of the issue',
        locationText: 'Original Location',
        urgency: ServiceRequestUrgency.low,
        status: ServiceRequestStatus.created,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.mockRequests = [sample];

      await tester.pumpWidget(
        buildApp(EditServiceRequestScreen(request: sample)),
      );
      await tester.pumpAndSettle();

      expect(find.text('Original description of the issue'), findsOneWidget);
      expect(find.text('Original Location'), findsOneWidget);

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Problem Description'),
        'Updated description with more specific details',
      );

      await tester.tap(find.text('Save Changes'));
      await tester.pumpAndSettle();

      expect(mockService.mockRequests.first.description,
          'Updated description with more specific details');
    });
  });
}
