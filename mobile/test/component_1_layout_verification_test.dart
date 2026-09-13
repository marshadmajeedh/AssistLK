import 'package:dio/dio.dart';
import 'package:mobile/features/service_requests/models/service_request_attachment.dart';
import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/api/api_client.dart';
import 'package:mobile/core/auth/token_storage.dart';
import 'package:mobile/features/auth/models/auth_user.dart';
import 'package:mobile/features/auth/providers/auth_provider.dart';
import 'package:mobile/features/auth/services/auth_service.dart';
import 'package:mobile/features/service_requests/models/create_service_request_dto.dart';
import 'package:mobile/features/service_requests/models/problem_analysis_summary_model.dart';
import 'package:mobile/features/service_requests/models/problem_understanding_result_model.dart';
import 'package:mobile/features/service_requests/models/service_request_model.dart';
import 'package:mobile/features/service_requests/models/service_request_status.dart';
import 'package:mobile/features/service_requests/models/service_request_urgency.dart';
import 'package:mobile/features/service_requests/models/update_service_request_dto.dart';
import 'package:mobile/features/service_requests/providers/service_request_provider.dart';
import 'package:mobile/features/service_requests/screens/create_service_request_screen.dart';
import 'package:mobile/features/service_requests/screens/customer_activity_screen.dart';
import 'package:mobile/features/service_requests/screens/customer_services_screen.dart';
import 'package:mobile/features/service_requests/screens/service_request_detail_screen.dart';
import 'package:mobile/features/service_requests/services/service_request_service.dart';
import 'package:mobile/features/service_requests/widgets/analysis_result_card.dart';
import 'package:mobile/features/service_requests/widgets/clarification_section.dart';
import 'package:mobile/features/service_requests/widgets/ready_for_matching_section.dart';
import 'package:mobile/shared/theme/app_theme.dart';
import 'package:provider/provider.dart';

class MockLayoutServiceRequestService extends ServiceRequestService {
  MockLayoutServiceRequestService() : super(apiClient: ApiClient());

  List<ServiceRequestModel> requests = [];
  ProblemUnderstandingResultModel? analysisResult;

  @override
  Future<List<ServiceRequestAttachment>> listAttachments(String id, {CancelToken? cancelToken}) async => [];

  @override
  Future<List<ServiceRequestModel>> getMyRequests() async => List.of(requests);

  @override
  Future<ServiceRequestModel> getById(String id) async =>
      requests.firstWhere((r) => r.serviceRequestId == id);

  @override
  Future<ServiceRequestModel> create(CreateServiceRequestDto dto) async {
    final created = ServiceRequestModel(
      serviceRequestId: 'req-created',
      customerId: 'cust-1',
      category: 'General',
      description: dto.description,
      locationText: dto.locationText,
      latitude: dto.latitude,
      longitude: dto.longitude,
      urgency: ServiceRequestUrgency.low,
      status: ServiceRequestStatus.created,
      createdAt: DateTime(2026, 9, 9),
      updatedAt: DateTime(2026, 9, 9),
    );
    requests.insert(0, created);
    return created;
  }

  @override
  Future<ServiceRequestModel> update(String id, UpdateServiceRequestDto dto) async {
    final idx = requests.indexWhere((r) => r.serviceRequestId == id);
    final updated = requests[idx].copyWith(
      description: dto.description,
      locationText: dto.locationText,
      latitude: dto.latitude,
      longitude: dto.longitude,
    );
    requests[idx] = updated;
    return updated;
  }

  @override
  Future<ProblemUnderstandingResultModel> analyze(String id) async {
    return analysisResult ??
        ProblemUnderstandingResultModel(
          workflowId: 'wf-101',
          executionId: 'ex-101',
          serviceRequestId: id,
          status: ServiceRequestStatus.analyzed,
          category: 'Electrical Wiring',
          problemSummary: 'Short circuit detected in main switchboard',
          urgency: ServiceRequestUrgency.high,
          confidence: 0.95,
          needsMoreInformation: false,
          followUpQuestions: const [],
        );
  }

  @override
  Future<ServiceRequestModel> markReadyForMatching(String id) async {
    final idx = requests.indexWhere((r) => r.serviceRequestId == id);
    final updated = requests[idx].copyWith(
      status: ServiceRequestStatus.readyForMatching,
    );
    requests[idx] = updated;
    return updated;
  }
}

class FakeLayoutAuthProvider extends AuthProvider {
  FakeLayoutAuthProvider()
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
  late MockLayoutServiceRequestService mockService;
  late ServiceRequestProvider requestProvider;
  late FakeLayoutAuthProvider authProvider;

  setUp(() {
    mockService = MockLayoutServiceRequestService();
    requestProvider = ServiceRequestProvider(serviceRequestService: mockService);
    authProvider = FakeLayoutAuthProvider();
  });

  Widget buildThemedApp(Widget child) {
    return MultiProvider(
      providers: [
        ChangeNotifierProvider<AuthProvider>.value(value: authProvider),
        ChangeNotifierProvider<ServiceRequestProvider>.value(value: requestProvider),
      ],
      child: MaterialApp(
        theme: AppTheme.lightTheme,
        home: child,
      ),
    );
  }

  Future<void> simulateMouseHoverSweep(WidgetTester tester) async {
    final gesture = await tester.createGesture(kind: PointerDeviceKind.mouse);
    await gesture.addPointer(location: Offset.zero);

    for (double y = 40; y <= 600; y += 40) {
      await gesture.moveTo(Offset(200, y));
      await tester.pump();
    }
    await gesture.removePointer();
  }

  group('Component 1 UI Layout & RenderBox Verification Under AppTheme.lightTheme', () {
    testWidgets('1. CustomerActivityScreen empty state renders and handles hover without exceptions',
        (tester) async {
      mockService.requests = [];

      await requestProvider.loadMyRequests();
      await tester.pumpWidget(buildThemedApp(const Scaffold(body: CustomerActivityScreen())));
      await tester.pumpAndSettle();

      expect(find.text('My Requests'), findsOneWidget);
      expect(find.text('No Service Requests Yet'), findsOneWidget);

      await simulateMouseHoverSweep(tester);
    });

    testWidgets(
        '2. CustomerActivityScreen populated request list renders and handles hover without exceptions',
        (tester) async {
      mockService.requests = [
        ServiceRequestModel(
          serviceRequestId: 'req-1',
          customerId: 'cust-1',
          category: 'Plumbing',
          description: 'Water leak under the kitchen sink',
          locationText: 'Colombo 03',
          urgency: ServiceRequestUrgency.medium,
          status: ServiceRequestStatus.created,
          createdAt: DateTime(2026, 9, 9),
          updatedAt: DateTime(2026, 9, 9),
        ),
      ];

      await requestProvider.loadMyRequests();
      await tester.pumpWidget(buildThemedApp(const Scaffold(body: CustomerActivityScreen())));
      await tester.pumpAndSettle();

      expect(find.text('My Requests'), findsOneWidget);
      expect(find.text('Water leak under the kitchen sink'), findsOneWidget);

      await simulateMouseHoverSweep(tester);
    });

    testWidgets('3. Create Request screen renders and handles hover without exceptions',
        (tester) async {
      await tester.pumpWidget(buildThemedApp(const CreateServiceRequestScreen()));
      await tester.pumpAndSettle();

      expect(find.text('Create Service Request'), findsOneWidget);
      expect(find.text('Next: Location'), findsOneWidget);

      await simulateMouseHoverSweep(tester);

      // Step to Location
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Problem Description'),
        'Major ceiling leak in the living room after heavy rain',
      );
      await tester.ensureVisible(find.text('Next: Location'));
      await tester.tap(find.text('Next: Location'));
      await tester.pumpAndSettle();

      expect(find.text('Next: Review'), findsOneWidget);
      await simulateMouseHoverSweep(tester);

      // Step to Review
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Location / Address'),
        '123 Galle Road, Colombo 03',
      );
      await tester.ensureVisible(find.text('Next: Review'));
      await tester.tap(find.text('Next: Review'));
      await tester.pumpAndSettle();

      expect(find.text('Submit Request'), findsOneWidget);
      await simulateMouseHoverSweep(tester);
    });

    testWidgets(
        '4. Detail screen in Created status renders Analyze button and handles hover without exceptions',
        (tester) async {
      final req = ServiceRequestModel(
        serviceRequestId: 'req-created-1',
        customerId: 'cust-1',
        category: 'Plumbing',
        description: 'Bathroom pipe issue',
        locationText: 'Galle',
        urgency: ServiceRequestUrgency.low,
        status: ServiceRequestStatus.created,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.requests = [req];

      await tester.pumpWidget(
        buildThemedApp(const ServiceRequestDetailScreen(requestId: 'req-created-1')),
      );
      await tester.pumpAndSettle();

      expect(find.text('Analyze with AssistLK AI'), findsOneWidget);
      expect(find.textContaining('Gemini'), findsNothing);
      await simulateMouseHoverSweep(tester);
    });

    testWidgets(
        '5. Detail screen in Analyzed status renders AnalysisResultCard and handles hover without exceptions',
        (tester) async {
      final req = ServiceRequestModel(
        serviceRequestId: 'req-analyzed-1',
        customerId: 'cust-1',
        category: 'Electrical',
        description: 'Short circuit in breaker',
        locationText: 'Colombo 05',
        urgency: ServiceRequestUrgency.high,
        status: ServiceRequestStatus.analyzed,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.requests = [req];

      await tester.pumpWidget(
        buildThemedApp(const ServiceRequestDetailScreen(requestId: 'req-analyzed-1')),
      );
      await tester.pumpAndSettle();

      expect(find.byType(AnalysisResultCard), findsOneWidget);
      expect(find.text('Mark Ready for Matching'), findsOneWidget);

      await simulateMouseHoverSweep(tester);
    });

    testWidgets(
        '6. Detail screen in AwaitingInformation status renders ClarificationSection and handles hover',
        (tester) async {
      final req = ServiceRequestModel(
        serviceRequestId: 'req-clarify-1',
        customerId: 'cust-1',
        category: 'Carpentry',
        description: 'Door lock jammed',
        locationText: 'Kandy',
        urgency: ServiceRequestUrgency.medium,
        status: ServiceRequestStatus.awaitingInformation,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.requests = [req];
      mockService.analysisResult = ProblemUnderstandingResultModel(
        workflowId: 'wf-1',
        executionId: 'ex-1',
        serviceRequestId: 'req-clarify-1',
        status: ServiceRequestStatus.awaitingInformation,
        category: 'Carpentry',
        problemSummary: 'Need more door details',
        urgency: ServiceRequestUrgency.medium,
        confidence: 0.70,
        needsMoreInformation: true,
        followUpQuestions: ['Is the lock electronic or mechanical?'],
      );

      await tester.pumpWidget(
        buildThemedApp(const ServiceRequestDetailScreen(requestId: 'req-clarify-1')),
      );
      await tester.pumpAndSettle();

      expect(find.byType(ClarificationSection), findsOneWidget);
      expect(find.text('Edit Details'), findsOneWidget);
      expect(find.text('Re-analyze'), findsOneWidget);

      await simulateMouseHoverSweep(tester);
    });

    testWidgets(
        '7. Detail screen in ReadyForMatching status renders ReadyForMatchingSection and handles hover',
        (tester) async {
      final req = ServiceRequestModel(
        serviceRequestId: 'req-ready-1',
        customerId: 'cust-1',
        category: 'Plumbing',
        description: 'Ready request description',
        locationText: 'Colombo 07',
        urgency: ServiceRequestUrgency.low,
        status: ServiceRequestStatus.readyForMatching,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.requests = [req];

      await tester.pumpWidget(
        buildThemedApp(const ServiceRequestDetailScreen(requestId: 'req-ready-1')),
      );
      await tester.pumpAndSettle();

      expect(find.byType(ReadyForMatchingSection), findsOneWidget);
      expect(find.text('Ready for provider matching'), findsOneWidget);

      await simulateMouseHoverSweep(tester);
    });
  });

  group('CustomerActivityScreen Responsive Layout Verification (320px, 360px, Desktop)', () {
    testWidgets('Renders safely on narrow 320px viewport without overflow or truncation',
        (tester) async {
      tester.view.physicalSize = const Size(320, 568);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      mockService.requests = [
        ServiceRequestModel(
          serviceRequestId: 'req-narrow-1',
          customerId: 'cust-1',
          category: 'Plumbing',
          description: 'Sink leak in Colombo',
          locationText: 'Colombo',
          urgency: ServiceRequestUrgency.medium,
          status: ServiceRequestStatus.created,
          createdAt: DateTime(2026, 9, 9),
          updatedAt: DateTime(2026, 9, 9),
        ),
      ];

      await requestProvider.loadMyRequests();
      await tester.pumpWidget(buildThemedApp(const Scaffold(body: CustomerActivityScreen())));
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
      expect(find.text('My Requests'), findsOneWidget);
      expect(find.widgetWithText(ElevatedButton, 'Create Request'), findsOneWidget);

      await tester.pumpWidget(buildThemedApp(const Scaffold(body: CustomerServicesScreen())));
      await tester.pumpAndSettle();
      expect(find.text('Not sure what service you need?'), findsOneWidget);
      expect(
        find.text(
          'Describe your issue in plain language and let AssistLK AI analyze your problem, identify the right service, and estimate urgency.',
        ),
        findsOneWidget,
      );
      expect(find.text('Let AssistLK AI analyze your problem'), findsOneWidget);
      expect(tester.takeException(), isNull);
    });

    testWidgets('Renders safely on 360px viewport without overflow with stacked actions', (tester) async {
      tester.view.physicalSize = const Size(360, 640);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      mockService.requests = [];

      await requestProvider.loadMyRequests();
      await tester.pumpWidget(buildThemedApp(const Scaffold(body: CustomerActivityScreen())));
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
      expect(find.text('My Requests'), findsOneWidget);
      expect(find.widgetWithText(ElevatedButton, 'Create Request').first, findsOneWidget);


      // Verify the narrow Activity header stacks its action.
      final titleCenter = tester.getCenter(find.text('My Requests'));
      final buttonCenter = tester.getCenter(find.widgetWithText(ElevatedButton, 'Create Request').first);
      expect(buttonCenter.dy, greaterThan(titleCenter.dy)); // Stack on narrow widths.
      await tester.pumpWidget(buildThemedApp(const Scaffold(body: CustomerServicesScreen())));
      await tester.pumpAndSettle();
      expect(find.text('Not sure what service you need?'), findsOneWidget);
      expect(
        find.text(
          'Describe your issue in plain language and let AssistLK AI analyze your problem, identify the right service, and estimate urgency.',
        ),
        findsOneWidget,
      );
      expect(find.text('Let AssistLK AI analyze your problem'), findsOneWidget);
      expect(tester.takeException(), isNull);
    });

    testWidgets('Renders on physical device viewport (390x844) with same-row alignment', (tester) async {
      tester.view.physicalSize = const Size(390, 844);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      mockService.requests = [];

      await requestProvider.loadMyRequests();
      await tester.pumpWidget(buildThemedApp(const Scaffold(body: CustomerActivityScreen())));
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
      expect(find.text('My Requests'), findsOneWidget);
      expect(find.widgetWithText(ElevatedButton, 'Create Request').first, findsOneWidget);

      final titleCenter = tester.getCenter(find.text('My Requests'));
      final buttonCenter = tester.getCenter(find.widgetWithText(ElevatedButton, 'Create Request').first);
      expect((titleCenter.dy - buttonCenter.dy).abs(), lessThan(5.0));
      expect(titleCenter.dx, lessThan(buttonCenter.dx));
    });

    testWidgets('Tapping Create Request navigates from responsive header', (tester) async {
      mockService.requests = [];

      await requestProvider.loadMyRequests();
      await tester.pumpWidget(buildThemedApp(const Scaffold(body: CustomerActivityScreen())));
      await tester.pumpAndSettle();

      final createBtn = find.widgetWithText(ElevatedButton, 'Create Request').first;
      await tester.ensureVisible(createBtn);
      await tester.tap(createBtn);
      await tester.pumpAndSettle();

      expect(find.text('Create Service Request'), findsOneWidget);
    });

    testWidgets('Tapping AI Assistance Card navigates to create request with AI identification',
        (tester) async {
      mockService.requests = [];

      await tester.pumpWidget(buildThemedApp(const Scaffold(body: CustomerServicesScreen())));
      await tester.pumpAndSettle();

      final aiCard = find.text('Not sure what service you need?');
      await tester.ensureVisible(aiCard);
      await tester.tap(aiCard);
      await tester.pumpAndSettle();

      expect(find.text('Create Service Request'), findsOneWidget);
      expect(find.text('Let AssistLK AI identify'), findsOneWidget);
    });

    testWidgets('Detail screen in Created status renders AssistLK AI elements safely on narrow 320px and 360px viewports',
        (tester) async {
      for (final size in const [Size(320, 568), Size(360, 640)]) {
        tester.view.physicalSize = size;
        tester.view.devicePixelRatio = 1.0;

        final createdReq = ServiceRequestModel(
          serviceRequestId: 'req-layout-created-${size.width.toInt()}',
          customerId: 'cust-1',
          category: 'Plumbing',
          description: 'Burst pipe under sink',
          locationText: 'Colombo 03',
          urgency: ServiceRequestUrgency.high,
          status: ServiceRequestStatus.created,
          createdAt: DateTime(2026, 9, 9),
          updatedAt: DateTime(2026, 9, 9),
        );
        mockService.requests = [createdReq];

        await tester.pumpWidget(
          buildThemedApp(ServiceRequestDetailScreen(
            key: ValueKey('created-${size.width.toInt()}'),
            requestId: createdReq.serviceRequestId,
          )),
        );
        await tester.pumpAndSettle();

        expect(tester.takeException(), isNull);
        expect(find.text('Next Step: AssistLK AI Analysis'), findsOneWidget);
        expect(find.text('Analyze with AssistLK AI'), findsOneWidget);
        expect(find.textContaining('Gemini'), findsNothing);

        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      }
    });

    testWidgets('Detail screen in Analyzed status renders AssistLK AI elements safely on narrow 320px and 360px viewports',
        (tester) async {
      for (final size in const [Size(320, 568), Size(360, 640)]) {
        tester.view.physicalSize = size;
        tester.view.devicePixelRatio = 1.0;

        final analyzedReq = ServiceRequestModel(
          serviceRequestId: 'req-layout-analyzed-${size.width.toInt()}',
          customerId: 'cust-1',
          category: 'Electrical Wiring',
          categoryHint: 'Plumbing Service',
          description: 'Sparks from switchboard',
          locationText: 'Colombo 05',
          urgency: ServiceRequestUrgency.critical,
          status: ServiceRequestStatus.analyzed,
          createdAt: DateTime(2026, 9, 9),
          updatedAt: DateTime(2026, 9, 9),
          latestAnalysis: ProblemAnalysisSummaryModel(
            id: 'ana-1',
            detectedProblem: 'Short circuit detected in main switchboard',
            confidence: 0.95,
            agentName: 'AssistLK AI',
            createdAt: DateTime(2026, 9, 9),
          ),
        );
        mockService.requests = [analyzedReq];

        await tester.pumpWidget(
          buildThemedApp(ServiceRequestDetailScreen(
            key: ValueKey('analyzed-${size.width.toInt()}'),
            requestId: analyzedReq.serviceRequestId,
          )),
        );
        await tester.pumpAndSettle();

        expect(tester.takeException(), isNull);
        expect(find.byType(AnalysisResultCard), findsOneWidget);
        expect(find.text('AssistLK AI Analysis'), findsOneWidget);
        expect(find.text('AssistLK AI classification:'), findsOneWidget);
        expect(find.textContaining('Gemini'), findsNothing);

        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      }
    });
  });
}
