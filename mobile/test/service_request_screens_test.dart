import 'dart:async';
import 'dart:ui';
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
import 'package:mobile/shared/theme/app_theme.dart';
import 'package:provider/provider.dart';

class MockServiceRequestService extends ServiceRequestService {
  MockServiceRequestService() : super(apiClient: ApiClient());

  List<ServiceRequestModel> mockRequests = [];
  ProblemUnderstandingResultModel? mockAnalysis;
  Completer<ProblemUnderstandingResultModel>? analyzeCompleter;
  bool shouldAnalyzeThrow = false;
  bool shouldGetByIdThrow = false;

  @override
  Future<List<ServiceRequestModel>> getMyRequests() async => List.of(mockRequests);

  @override
  Future<ServiceRequestModel> getById(String id) async {
    if (shouldGetByIdThrow) throw Exception('GetById failed');
    return mockRequests.firstWhere((r) => r.serviceRequestId == id);
  }

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
    if (shouldAnalyzeThrow) throw Exception('Analysis failed');
    if (analyzeCompleter != null) {
      return await analyzeCompleter!.future;
    }
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
        theme: AppTheme.lightTheme,
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

      expect(tester.takeException(), isNull);
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

      expect(tester.takeException(), isNull);
      expect(find.text('Plumbing'), findsOneWidget);
      expect(find.text('Bathroom tap is dripping continuously'), findsOneWidget);
      expect(find.text('Created'), findsOneWidget);
      expect(find.text('Low'), findsOneWidget);
    });

    testWidgets('header Create Request button has finite content-safe constraints and survives mouse hover on desktop viewport',
        (tester) async {
      tester.view.physicalSize = const Size(1280, 800);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      await tester.pumpWidget(buildApp(const CustomerHomeScreen()));
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);

      final headerButtonFinder = find.widgetWithText(ElevatedButton, 'Create Request').first;
      expect(headerButtonFinder, findsOneWidget);

      // Verify the header button RenderBox has finite, content-safe dimensions
      final RenderBox renderBox = tester.renderObject(headerButtonFinder);
      expect(renderBox.hasSize, isTrue);
      expect(renderBox.size.width.isFinite, isTrue);
      expect(renderBox.size.height.isFinite, isTrue);
      expect(renderBox.size.width, greaterThan(0));
      expect(renderBox.size.width, lessThan(1280));
      expect(renderBox.constraints.hasBoundedWidth, isFalse); // Parent Row provides unbounded max width
      expect(renderBox.constraints.minWidth, 0.0); // Never infinite minimum width!

      // Simulate mouse hover and movement over the button
      final gesture = await tester.createGesture(kind: PointerDeviceKind.mouse);
      await gesture.addPointer(location: Offset.zero);
      await gesture.moveTo(tester.getCenter(headerButtonFinder));
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);

      await gesture.removePointer();
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
    });

    testWidgets('renders cleanly on narrow viewport without overflow or layout exceptions',
        (tester) async {
      tester.view.physicalSize = const Size(360, 640);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

      await tester.pumpWidget(buildApp(const CustomerHomeScreen()));
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
      expect(find.text('My Service Requests'), findsOneWidget);
      expect(find.widgetWithText(ElevatedButton, 'Create Request').first, findsOneWidget);
    });

    testWidgets('populated state renders cleanly on desktop viewport with pointer movement',
        (tester) async {
      tester.view.physicalSize = const Size(1280, 800);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(() {
        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      });

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

      expect(tester.takeException(), isNull);

      final headerButtonFinder = find.widgetWithText(ElevatedButton, 'Create Request').first;
      final gesture = await tester.createGesture(kind: PointerDeviceKind.mouse);
      await gesture.addPointer(location: Offset.zero);
      await gesture.moveTo(tester.getCenter(headerButtonFinder));
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);

      // Move over the card
      await gesture.moveTo(tester.getCenter(find.text('Plumbing')));
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);

      await gesture.removePointer();
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
    });

    testWidgets(
        'handles mouse hover and pointer movement across header action button and list without hit test layout errors',
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

      final gesture = await tester.createGesture(kind: PointerDeviceKind.mouse);
      await gesture.addPointer(location: Offset.zero);
      addTearDown(gesture.removePointer);

      // Hover over header action button and request cards
      await gesture.moveTo(tester.getCenter(find.text('Create Request').first));
      await tester.pump();
      await gesture.moveTo(tester.getCenter(find.text('My Service Requests')));
      await tester.pump();
      await gesture.moveTo(tester.getCenter(find.text('Bathroom tap is dripping continuously')));
      await tester.pump();
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

    testWidgets('ServiceRequestDetailScreen_WhenBackendStatusAnalyzing shows progress indicator and does not show Analyze button',
        (tester) async {
      final sample = ServiceRequestModel(
        serviceRequestId: 'req-analyzing',
        customerId: 'cust-1',
        category: 'Electrical',
        description: 'Breaker keeps tripping',
        locationText: 'Colombo',
        urgency: ServiceRequestUrgency.medium,
        status: ServiceRequestStatus.analyzing,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.mockRequests = [sample];

      await tester.pumpWidget(
        buildApp(const ServiceRequestDetailScreen(requestId: 'req-analyzing')),
      );
      await tester.pump();
      await tester.pump();

      expect(find.text('AI analysis in progress'), findsOneWidget);
      expect(find.byType(CircularProgressIndicator), findsOneWidget);
      expect(find.text('Analyze with Gemini AI'), findsNothing);
      expect(find.byIcon(Icons.cancel_outlined), findsNothing);
    });

    testWidgets('ServiceRequestDetailScreen_WhenProviderIsAnalyzing shows progress indicator and does not show Analyze button',
        (tester) async {
      final sample = ServiceRequestModel(
        serviceRequestId: 'req-c-loading',
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
      mockService.analyzeCompleter = Completer<ProblemUnderstandingResultModel>();

      await tester.pumpWidget(
        buildApp(const ServiceRequestDetailScreen(requestId: 'req-c-loading')),
      );
      await tester.pumpAndSettle();

      expect(find.text('Analyze with Gemini AI'), findsOneWidget);

      // Tap Analyze with Gemini AI
      await tester.tap(find.text('Analyze with Gemini AI'));
      await tester.pump();

      // Should show in-progress indicator and hide Analyze button
      expect(find.text('AI analysis in progress'), findsOneWidget);
      expect(find.byType(CircularProgressIndicator), findsOneWidget);
      expect(find.text('Analyze with Gemini AI'), findsNothing);
      expect(find.byIcon(Icons.cancel_outlined), findsNothing);

      // Complete analysis
      mockService.analyzeCompleter!.complete(
        ProblemUnderstandingResultModel(
          workflowId: 'wf-1',
          executionId: 'ex-1',
          serviceRequestId: 'req-c-loading',
          status: ServiceRequestStatus.analyzed,
          category: 'Electrical',
          problemSummary: 'Faulty circuit breaker detected',
          urgency: ServiceRequestUrgency.high,
          confidence: 0.96,
          needsMoreInformation: false,
          followUpQuestions: const [],
        ),
      );
      await tester.pumpAndSettle();

      expect(find.byType(AnalysisResultCard), findsOneWidget);
    });

    testWidgets('Successful clarification result sets AwaitingInformation and displays clarification state',
        (tester) async {
      final sample = ServiceRequestModel(
        serviceRequestId: 'req-clarify',
        customerId: 'cust-1',
        category: 'Plumbing',
        description: 'Strange sounds from radiator',
        locationText: 'Colombo',
        urgency: ServiceRequestUrgency.medium,
        status: ServiceRequestStatus.created,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.mockRequests = [sample];
      mockService.mockAnalysis = ProblemUnderstandingResultModel(
        workflowId: 'wf-clarify',
        executionId: 'ex-clarify',
        serviceRequestId: 'req-clarify',
        status: ServiceRequestStatus.awaitingInformation,
        category: 'Plumbing',
        problemSummary: 'Radiator knocking noise',
        urgency: ServiceRequestUrgency.medium,
        confidence: 0.85,
        needsMoreInformation: true,
        followUpQuestions: const [
          'Is the noise continuous or only when heat turns on?',
        ],
      );

      await tester.pumpWidget(
        buildApp(const ServiceRequestDetailScreen(requestId: 'req-clarify')),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.text('Analyze with Gemini AI'));
      await tester.pumpAndSettle();

      expect(find.byType(ClarificationSection), findsOneWidget);
      expect(find.text('Clarification Needed'), findsOneWidget);
      expect(find.text('Is the noise continuous or only when heat turns on?'),
          findsOneWidget);
      expect(find.text('Edit Details'), findsOneWidget);
      expect(find.text('Re-analyze'), findsOneWidget);
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

    testWidgets('analysisStateNeedsRefresh true shows uncertain card, hides Analyze, Edit, Cancel',
        (tester) async {
      final sample = ServiceRequestModel(
        serviceRequestId: 'req-uncertain',
        customerId: 'cust-1',
        category: 'General',
        description: 'Need assistance with appliance',
        locationText: 'Colombo',
        urgency: ServiceRequestUrgency.low,
        status: ServiceRequestStatus.created,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.mockRequests = [sample];

      await tester.pumpWidget(
        buildApp(const ServiceRequestDetailScreen(requestId: 'req-uncertain')),
      );
      await tester.pumpAndSettle();

      expect(find.text('Analyze with Gemini AI'), findsOneWidget);

      // Trigger analysis where both analyze and refresh fail
      mockService.shouldAnalyzeThrow = true;
      mockService.shouldGetByIdThrow = true;

      await tester.tap(find.text('Analyze with Gemini AI'));
      await tester.pumpAndSettle();

      // State is now uncertain
      expect(find.text('Unable to confirm the latest analysis status.'), findsOneWidget);
      expect(find.text('Refresh Status'), findsOneWidget);
      expect(find.text('Analyze with Gemini AI'), findsNothing);
      expect(find.text('Edit Details'), findsNothing);
      expect(find.byIcon(Icons.cancel_outlined), findsNothing);
    });

    testWidgets('manual Refresh Status restores Analyze button if backend returned Created',
        (tester) async {
      final sample = ServiceRequestModel(
        serviceRequestId: 'req-ref-c',
        customerId: 'cust-1',
        category: 'General',
        description: 'Need assistance with appliance',
        locationText: 'Colombo',
        urgency: ServiceRequestUrgency.low,
        status: ServiceRequestStatus.created,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.mockRequests = [sample];

      await tester.pumpWidget(
        buildApp(const ServiceRequestDetailScreen(requestId: 'req-ref-c')),
      );
      await tester.pumpAndSettle();

      mockService.shouldAnalyzeThrow = true;
      mockService.shouldGetByIdThrow = true;

      await tester.tap(find.text('Analyze with Gemini AI'));
      await tester.pumpAndSettle();

      expect(find.text('Refresh Status'), findsOneWidget);

      // Now network recovers and backend returns Created
      mockService.shouldGetByIdThrow = false;

      await tester.tap(find.text('Refresh Status'));
      await tester.pumpAndSettle();

      expect(find.text('Analyze with Gemini AI'), findsOneWidget);
      expect(find.text('Unable to confirm the latest analysis status.'), findsNothing);
    });

    testWidgets('manual Refresh Status shows progress state if backend returned Analyzing',
        (tester) async {
      final sample = ServiceRequestModel(
        serviceRequestId: 'req-ref-a',
        customerId: 'cust-1',
        category: 'General',
        description: 'Need assistance with appliance',
        locationText: 'Colombo',
        urgency: ServiceRequestUrgency.low,
        status: ServiceRequestStatus.created,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.mockRequests = [sample];

      await tester.pumpWidget(
        buildApp(const ServiceRequestDetailScreen(requestId: 'req-ref-a')),
      );
      await tester.pumpAndSettle();

      mockService.shouldAnalyzeThrow = true;
      mockService.shouldGetByIdThrow = true;

      await tester.tap(find.text('Analyze with Gemini AI'));
      await tester.pumpAndSettle();

      // Backend actually transitioned to Analyzing
      mockService.shouldGetByIdThrow = false;
      mockService.mockRequests = [
        sample.copyWith(status: ServiceRequestStatus.analyzing),
      ];

      await tester.tap(find.text('Refresh Status'));
      await tester.pump();
      await tester.pump();

      expect(find.text('AI analysis in progress'), findsOneWidget);
      expect(find.byType(CircularProgressIndicator), findsOneWidget);
      expect(find.text('Analyze with Gemini AI'), findsNothing);
    });

    testWidgets('manual Refresh Status shows clarification state if backend returned AwaitingInformation',
        (tester) async {
      final sample = ServiceRequestModel(
        serviceRequestId: 'req-ref-ai',
        customerId: 'cust-1',
        category: 'General',
        description: 'Need assistance with appliance',
        locationText: 'Colombo',
        urgency: ServiceRequestUrgency.low,
        status: ServiceRequestStatus.created,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.mockRequests = [sample];

      await tester.pumpWidget(
        buildApp(const ServiceRequestDetailScreen(requestId: 'req-ref-ai')),
      );
      await tester.pumpAndSettle();

      mockService.shouldAnalyzeThrow = true;
      mockService.shouldGetByIdThrow = true;

      await tester.tap(find.text('Analyze with Gemini AI'));
      await tester.pumpAndSettle();

      // Backend finished with AwaitingInformation
      mockService.shouldGetByIdThrow = false;
      mockService.mockRequests = [
        sample.copyWith(status: ServiceRequestStatus.awaitingInformation),
      ];

      await tester.tap(find.text('Refresh Status'));
      await tester.pumpAndSettle();

      expect(find.byType(ClarificationSection), findsOneWidget);
      expect(find.text('Clarification Needed'), findsOneWidget);
      expect(find.text('Analyze with Gemini AI'), findsNothing);
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
