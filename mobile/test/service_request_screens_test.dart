import 'dart:async';
import 'dart:ui';
import 'package:dio/dio.dart';
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
import 'package:mobile/features/service_requests/services/location_service.dart';
import 'package:mobile/features/service_requests/services/service_request_service.dart';
import 'mocks/mock_location_service.dart';
import 'package:mobile/features/service_requests/widgets/analysis_result_card.dart';
import 'package:mobile/features/service_requests/widgets/clarification_section.dart';
import 'package:mobile/features/service_requests/widgets/ready_for_matching_section.dart';
import 'package:mobile/features/service_requests/widgets/service_category_card.dart';
import 'package:mobile/features/service_requests/widgets/service_request_card.dart';
import 'package:mobile/shared/theme/app_theme.dart';
import 'package:provider/provider.dart';

class MockServiceRequestService extends ServiceRequestService {
  MockServiceRequestService() : super(apiClient: ApiClient());

  List<ServiceRequestModel> mockRequests = [];
  ProblemUnderstandingResultModel? mockAnalysis;
  Completer<ProblemUnderstandingResultModel>? analyzeCompleter;
  bool shouldAnalyzeThrow = false;
  Exception? analyzeException;
  bool shouldGetByIdThrow = false;
  List<ServiceRequestModel>? getByIdResponses;
  CreateServiceRequestDto? lastCreateDto;
  UpdateServiceRequestDto? lastUpdateDto;

  @override
  Future<List<ServiceRequestModel>> getMyRequests() async => List.of(mockRequests);

  @override
  Future<ServiceRequestModel> getById(String id) async {
    if (shouldGetByIdThrow) throw Exception('GetById failed');
    if (getByIdResponses != null && getByIdResponses!.isNotEmpty) {
      return getByIdResponses!.removeAt(0);
    }
    return mockRequests.firstWhere((r) => r.serviceRequestId == id);
  }

  @override
  Future<ServiceRequestModel> create(CreateServiceRequestDto dto) async {
    lastCreateDto = dto;
    final created = ServiceRequestModel(
      serviceRequestId: 'req-new-${mockRequests.length + 1}',
      customerId: 'cust-1',
      category: 'General',
      categoryHint: dto.categoryHint,
      description: dto.description,
      locationText: dto.locationText,
      latitude: dto.latitude,
      longitude: dto.longitude,
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
    lastUpdateDto = dto;
    final idx = mockRequests.indexWhere((r) => r.serviceRequestId == id);
    final updated = mockRequests[idx].copyWith(
      categoryHint: dto.categoryHint,
      description: dto.description,
      locationText: dto.locationText,
      latitude: dto.latitude,
      longitude: dto.longitude,
    );
    mockRequests[idx] = updated;
    return updated;
  }

  @override
  Future<ProblemUnderstandingResultModel> analyze(String id) async {
    if (analyzeException != null) throw analyzeException!;
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
    requestProvider = ServiceRequestProvider(
      serviceRequestService: mockService,
      reconciliationPollInterval: Duration.zero,
      maxReconciliationPolls: 3,
    );
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
      expect(find.widgetWithText(ServiceCategoryCard, 'Plumbing'), findsOneWidget);
      expect(find.widgetWithText(ServiceRequestCard, 'Plumbing'), findsOneWidget);
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
      expect(find.text('My Requests'), findsOneWidget);
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
      await gesture.moveTo(tester.getCenter(find.widgetWithText(ServiceRequestCard, 'Plumbing')));
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
      await gesture.moveTo(tester.getCenter(find.text('My Requests')));
      await tester.pump();
      await gesture.moveTo(tester.getCenter(find.text('Bathroom tap is dripping continuously')));
      await tester.pump();
    });
  });

  group('CreateServiceRequestScreen 3-Step Guided Flow', () {
    testWidgets('validates required inputs and submits across 3-step flow', (tester) async {
      await tester.pumpWidget(buildApp(const CreateServiceRequestScreen()));
      await tester.pumpAndSettle();

      expect(find.text('Create Service Request'), findsOneWidget);
      expect(find.text('Next: Location'), findsOneWidget);

      // Step 1: Tap Next without typing description -> shows validation error
      await tester.tap(find.text('Next: Location'));
      await tester.pumpAndSettle();

      expect(find.text('Please describe the problem you are experiencing.'), findsOneWidget);

      // Enter description with fewer than 10 characters
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Problem Description'),
        'Short',
      );
      await tester.tap(find.text('Next: Location'));
      await tester.pumpAndSettle();

      expect(find.text('Please provide at least 10 characters.'), findsOneWidget);

      // Enter valid description
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Problem Description'),
        'Major ceiling leak in the living room after heavy rain',
      );

      // Advance to Step 2: Location
      await tester.tap(find.text('Next: Location'));
      await tester.pumpAndSettle();

      expect(find.text('Service Location'), findsOneWidget);
      expect(find.text('Next: Review'), findsOneWidget);

      // Step 2: Tap Next without typing location -> shows validation error
      await tester.tap(find.text('Next: Review'));
      await tester.pumpAndSettle();

      expect(find.text('Please provide the service location.'), findsOneWidget);

      // Enter valid location
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Location / Address'),
        '123 Galle Road, Colombo 03',
      );

      // Advance to Step 3: Review
      await tester.tap(find.text('Next: Review'));
      await tester.pumpAndSettle();

      expect(find.text('Review Service Request'), findsOneWidget);
      expect(find.text('Submit Request'), findsOneWidget);
      expect(
        find.text(
          'AssistLK AI will analyze your description and confirm the appropriate service category and urgency.',
        ),
        findsOneWidget,
      );

      // Submit Request
      final submitButton = find.text('Submit Request');
      await tester.ensureVisible(submitButton);
      await tester.tap(submitButton);
      await tester.pumpAndSettle();

      expect(mockService.mockRequests.length, 1);
      expect(
        mockService.mockRequests.first.description,
        'Major ceiling leak in the living room after heavy rain',
      );
      expect(mockService.mockRequests.first.locationText, '123 Galle Road, Colombo 03');
    });

    testWidgets('supports back navigation between steps while preserving entered data',
        (tester) async {
      await tester.pumpWidget(buildApp(const CreateServiceRequestScreen()));
      await tester.pumpAndSettle();

      // Step 1: Enter description
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Problem Description'),
        'Broken pipe leaking continuously under kitchen sink',
      );
      await tester.tap(find.text('Next: Location'));
      await tester.pumpAndSettle();

      // Step 2: Enter location
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Location / Address'),
        '45 Havelock Road, Colombo 05',
      );

      // Navigate back to Step 1
      await tester.tap(find.widgetWithText(OutlinedButton, 'Back'));
      await tester.pumpAndSettle();

      expect(find.text('Next: Location'), findsOneWidget);
      expect(
        find.text('Broken pipe leaking continuously under kitchen sink'),
        findsOneWidget,
      );

      // Navigate forward to Step 2 again
      await tester.tap(find.text('Next: Location'));
      await tester.pumpAndSettle();

      expect(find.text('45 Havelock Road, Colombo 05'), findsOneWidget);

      // Advance to Step 3
      await tester.tap(find.text('Next: Review'));
      await tester.pumpAndSettle();

      expect(find.text('Review Service Request'), findsOneWidget);

      // Navigate back to Step 2
      final backButton = find.widgetWithText(OutlinedButton, 'Back');
      await tester.ensureVisible(backButton);
      await tester.tap(backButton);
      await tester.pumpAndSettle();

      expect(find.text('Service Location'), findsOneWidget);
      expect(find.text('45 Havelock Road, Colombo 05'), findsOneWidget);
    });

    testWidgets('renders initial category preference banner and supports changing preference',
        (tester) async {
      await tester.pumpWidget(
        buildApp(
          const CreateServiceRequestScreen(
            initialCategoryPreference: 'Plumbing',
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Service preference'), findsOneWidget);
      expect(find.text('Plumbing'), findsOneWidget);
      expect(find.text('Change'), findsOneWidget);

      // Tap Change to open bottom sheet
      await tester.tap(find.text('Change'));
      await tester.pumpAndSettle();

      expect(find.text('Select Service Preference'), findsOneWidget);
      expect(find.text('Electrical'), findsOneWidget);

      // Select Electrical
      await tester.tap(find.text('Electrical'));
      await tester.pumpAndSettle();

      expect(find.text('Electrical'), findsOneWidget);
    });

    testWidgets('renders Let AssistLK AI identify banner when no preference is provided',
        (tester) async {
      await tester.pumpWidget(
        buildApp(const CreateServiceRequestScreen(initialCategoryPreference: null)),
      );
      await tester.pumpAndSettle();

      expect(find.text('Service preference'), findsOneWidget);
      expect(find.text('Let AssistLK AI identify'), findsOneWidget);
      expect(find.text('Choose preference'), findsOneWidget);
    });
  });

  group('CustomerHomeScreen Service Shortcuts and AI Option', () {
    testWidgets('renders exactly 4 canonical category shortcuts and no prohibited categories',
        (tester) async {
      await tester.pumpWidget(buildApp(const CustomerHomeScreen()));
      await tester.pumpAndSettle();

      expect(find.text('What do you need help with?'), findsOneWidget);
      expect(find.widgetWithText(ServiceCategoryCard, 'Plumbing'), findsOneWidget);
      expect(find.widgetWithText(ServiceCategoryCard, 'Electrical'), findsOneWidget);
      expect(find.widgetWithText(ServiceCategoryCard, 'Vehicle Assistance'), findsOneWidget);
      expect(find.widgetWithText(ServiceCategoryCard, 'Appliance Repair'), findsOneWidget);

      // Verify prohibited categories are NEVER rendered
      expect(find.text('Cleaning'), findsNothing);
      expect(find.text('AC Service'), findsNothing);
      expect(find.text('Carpentry'), findsNothing);
      expect(find.text('Gardening'), findsNothing);
      expect(find.text('Work'), findsNothing);
      expect(find.text('Emergency'), findsNothing);
    });

    testWidgets('renders prominent AI problem understanding card', (tester) async {
      await tester.pumpWidget(buildApp(const CustomerHomeScreen()));
      await tester.pumpAndSettle();

      expect(find.text('Not sure what service you need?'), findsOneWidget);
      expect(find.text('Let AssistLK AI analyze your problem'), findsOneWidget);
    });

    testWidgets('tapping Plumbing shortcut navigates with Plumbing preference',
        (tester) async {
      await tester.pumpWidget(buildApp(const CustomerHomeScreen()));
      await tester.pumpAndSettle();

      await tester.tap(find.widgetWithText(ServiceCategoryCard, 'Plumbing'));
      await tester.pumpAndSettle();

      expect(find.text('Create Service Request'), findsOneWidget);
      expect(find.text('Plumbing'), findsOneWidget);
    });

    testWidgets('tapping Vehicle Assistance shortcut navigates with canonical Vehicle Repair',
        (tester) async {
      await tester.pumpWidget(buildApp(const CustomerHomeScreen()));
      await tester.pumpAndSettle();

      await tester.tap(find.widgetWithText(ServiceCategoryCard, 'Vehicle Assistance'));
      await tester.pumpAndSettle();

      expect(find.text('Create Service Request'), findsOneWidget);
      expect(find.text('Vehicle Assistance'), findsOneWidget);
    });

    testWidgets('tapping AI option card navigates with Let AssistLK AI identify mode',
        (tester) async {
      await tester.pumpWidget(buildApp(const CustomerHomeScreen()));
      await tester.pumpAndSettle();

      final aiCardFinder = find.text('Not sure what service you need?');
      await tester.ensureVisible(aiCardFinder);
      await tester.tap(aiCardFinder);
      await tester.pumpAndSettle();

      expect(find.text('Create Service Request'), findsOneWidget);
      expect(find.text('Let AssistLK AI identify'), findsOneWidget);
    });
  });

  group('ServiceRequestDetailScreen Status-Driven Logic', () {
    testWidgets('Created status shows Analyze with AssistLK AI button', (tester) async {
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
      expect(find.text('Analyze with AssistLK AI'), findsOneWidget);
      expect(find.textContaining('Gemini'), findsNothing);

      // Trigger analysis
      await tester.tap(find.text('Analyze with AssistLK AI'));
      await tester.pumpAndSettle();

      expect(find.byType(AnalysisResultCard), findsOneWidget);
      expect(find.textContaining('Gemini'), findsNothing);
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

      expect(find.text('AssistLK AI analysis in progress'), findsOneWidget);
      expect(find.byType(CircularProgressIndicator), findsOneWidget);
      expect(find.text('Analyze with AssistLK AI'), findsNothing);
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

      expect(find.text('Analyze with AssistLK AI'), findsOneWidget);

      // Tap Analyze with AssistLK AI
      await tester.tap(find.text('Analyze with AssistLK AI'));
      await tester.pump();

      // Should show in-progress indicator and hide Analyze button
      expect(find.text('AssistLK AI analysis in progress'), findsOneWidget);
      expect(find.byType(CircularProgressIndicator), findsOneWidget);
      expect(find.text('Analyze with AssistLK AI'), findsNothing);
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

      await tester.tap(find.text('Analyze with AssistLK AI'));
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

      expect(find.text('Analyze with AssistLK AI'), findsOneWidget);

      // Trigger analysis where both analyze and refresh fail
      mockService.shouldAnalyzeThrow = true;
      mockService.shouldGetByIdThrow = true;

      await tester.tap(find.text('Analyze with AssistLK AI'));
      await tester.pumpAndSettle();

      // State is now uncertain
      expect(find.text('Unable to confirm the latest analysis status.'), findsOneWidget);
      expect(find.text('Refresh Status'), findsOneWidget);
      expect(find.text('Analyze with AssistLK AI'), findsNothing);
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

      await tester.tap(find.text('Analyze with AssistLK AI'));
      await tester.pumpAndSettle();

      expect(find.text('Refresh Status'), findsOneWidget);

      // Now network recovers and backend returns Created
      mockService.shouldGetByIdThrow = false;

      await tester.tap(find.text('Refresh Status'));
      await tester.pumpAndSettle();

      expect(find.text('Analyze with AssistLK AI'), findsOneWidget);
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

      await tester.tap(find.text('Analyze with AssistLK AI'));
      await tester.pumpAndSettle();

      // Backend actually transitioned to Analyzing
      mockService.shouldGetByIdThrow = false;
      mockService.mockRequests = [
        sample.copyWith(status: ServiceRequestStatus.analyzing),
      ];

      await tester.tap(find.text('Refresh Status'));
      await tester.pump();
      await tester.pump();

      expect(find.text('AssistLK AI analysis in progress'), findsOneWidget);
      expect(find.byType(CircularProgressIndicator), findsOneWidget);
      expect(find.text('Analyze with AssistLK AI'), findsNothing);
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

      await tester.tap(find.text('Analyze with AssistLK AI'));
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
      expect(find.text('Analyze with AssistLK AI'), findsNothing);
    });

    testWidgets('timeout followed by reconciliation reaching Analyzed shows AnalysisResultCard and mismatch banner',
        (tester) async {
      final sample = ServiceRequestModel(
        serviceRequestId: 'req-rec-a',
        customerId: 'cust-1',
        category: 'Electrical',
        categoryHint: 'Electrical',
        description: 'Water is leaking heavily from the pipe under my kitchen sink.',
        locationText: 'Colombo',
        urgency: ServiceRequestUrgency.low,
        status: ServiceRequestStatus.created,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.mockRequests = [sample];

      await tester.pumpWidget(
        buildApp(const ServiceRequestDetailScreen(requestId: 'req-rec-a')),
      );
      await tester.pumpAndSettle();

      expect(find.text('Analyze with AssistLK AI'), findsOneWidget);

      mockService.analyzeException = DioException(
        requestOptions: RequestOptions(path: '/service-requests/req-rec-a/analyze'),
        type: DioExceptionType.receiveTimeout,
      );
      mockService.getByIdResponses = [
        sample.copyWith(status: ServiceRequestStatus.analyzing),
        sample.copyWith(
          status: ServiceRequestStatus.analyzed,
          category: 'Plumbing',
          urgency: ServiceRequestUrgency.high,
        ),
      ];

      await tester.tap(find.text('Analyze with AssistLK AI'));
      await tester.pumpAndSettle();

      // Successfully transitioned to Analyzed via bounded reconciliation
      expect(find.byType(AnalysisResultCard), findsOneWidget);
      expect(find.text('Plumbing'), findsWidgets);
      expect(find.text('Your preference:'), findsOneWidget);
      expect(find.text('Electrical'), findsWidgets);
      expect(find.text('AssistLK AI identified a different service category based on your problem description.'), findsOneWidget);
      expect(find.text('Mark Ready for Matching'), findsOneWidget);
      expect(find.text('Analyze with AssistLK AI'), findsNothing);
      expect(find.byType(SnackBar), findsNothing);
    });

    testWidgets('timeout followed by grace-period expiry shows informational processing card, no red error, and Refresh Status button',
        (tester) async {
      final sample = ServiceRequestModel(
        serviceRequestId: 'req-grace',
        customerId: 'cust-1',
        category: 'General',
        description: 'Strange noise in pipes',
        locationText: 'Colombo',
        urgency: ServiceRequestUrgency.low,
        status: ServiceRequestStatus.created,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.mockRequests = [sample];

      await tester.pumpWidget(
        buildApp(const ServiceRequestDetailScreen(requestId: 'req-grace')),
      );
      await tester.pumpAndSettle();

      expect(find.text('Analyze with AssistLK AI'), findsOneWidget);

      mockService.mockRequests = [
        sample.copyWith(status: ServiceRequestStatus.analyzing),
      ];
      mockService.analyzeException = DioException(
        requestOptions: RequestOptions(path: '/service-requests/req-grace/analyze'),
        type: DioExceptionType.receiveTimeout,
      );

      await tester.tap(find.text('Analyze with AssistLK AI'));
      await tester.pump();
      await tester.pump();
      await tester.pump();

      // Grace period expired while still Analyzing: informational processing card with Refresh Status
      expect(find.text('AssistLK AI analysis in progress'), findsOneWidget);
      expect(find.byType(CircularProgressIndicator), findsOneWidget);
      expect(
        find.text('Analysis is taking longer than expected. Your request is still being processed.'),
        findsOneWidget,
      );
      expect(find.text('Refresh Status'), findsOneWidget);
      expect(find.text('Analyze with AssistLK AI'), findsNothing);
      expect(find.byIcon(Icons.cancel_outlined), findsNothing);
      expect(find.byType(SnackBar), findsNothing);
    });

    testWidgets('manual Refresh Status from grace-period expiry recovers to Analyzed status',
        (tester) async {
      final sample = ServiceRequestModel(
        serviceRequestId: 'req-recover-a',
        customerId: 'cust-1',
        category: 'General',
        description: 'Sink leak',
        locationText: 'Colombo',
        urgency: ServiceRequestUrgency.low,
        status: ServiceRequestStatus.created,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.mockRequests = [sample];

      await tester.pumpWidget(
        buildApp(const ServiceRequestDetailScreen(requestId: 'req-recover-a')),
      );
      await tester.pumpAndSettle();

      expect(find.text('Analyze with AssistLK AI'), findsOneWidget);

      mockService.mockRequests = [
        sample.copyWith(status: ServiceRequestStatus.analyzing),
      ];
      mockService.analyzeException = DioException(
        requestOptions: RequestOptions(path: '/service-requests/req-recover-a/analyze'),
        type: DioExceptionType.receiveTimeout,
      );

      await tester.tap(find.text('Analyze with AssistLK AI'));
      await tester.pump();
      await tester.pump();
      await tester.pump();

      expect(find.text('Refresh Status'), findsOneWidget);

      // Backend completes analysis
      mockService.analyzeException = null;
      mockService.mockRequests = [
        sample.copyWith(
          status: ServiceRequestStatus.analyzed,
          category: 'Plumbing',
          urgency: ServiceRequestUrgency.high,
        ),
      ];

      await tester.tap(find.text('Refresh Status'));
      await tester.pumpAndSettle();

      expect(find.byType(AnalysisResultCard), findsOneWidget);
      expect(
        find.text('Analysis is taking longer than expected. Your request is still being processed.'),
        findsNothing,
      );
      expect(find.text('Mark Ready for Matching'), findsOneWidget);
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

  group('CreateServiceRequestScreen CategoryHint Integration', () {
    testWidgets('submits canonical categoryHint for Plumbing and preserves description', (tester) async {
      await tester.pumpWidget(
        buildApp(const CreateServiceRequestScreen(initialCategoryPreference: 'Plumbing')),
      );
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Problem Description'),
        'Bathroom pipe leaking under the washbasin',
      );
      await tester.tap(find.text('Next: Location'));
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Location / Address'),
        'No 10 Main Street, Kandy',
      );
      await tester.tap(find.text('Next: Review'));
      await tester.pumpAndSettle();

      expect(find.text('Plumbing'), findsWidgets);

      final submitButton = find.text('Submit Request');
      await tester.ensureVisible(submitButton);
      await tester.tap(submitButton);
      await tester.pumpAndSettle();

      expect(mockService.lastCreateDto?.categoryHint, 'Plumbing');
      expect(mockService.lastCreateDto?.description, 'Bathroom pipe leaking under the washbasin');
      expect(mockService.lastCreateDto?.locationText, 'No 10 Main Street, Kandy');
    });

    testWidgets('submits Vehicle Assistance mapped to canonical Vehicle Repair', (tester) async {
      await tester.pumpWidget(
        buildApp(const CreateServiceRequestScreen(initialCategoryPreference: 'Vehicle Assistance')),
      );
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Problem Description'),
        'Car engine stalling while idling at traffic lights',
      );
      await tester.tap(find.text('Next: Location'));
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Location / Address'),
        'Galle Road, Bambalapitiya',
      );
      await tester.tap(find.text('Next: Review'));
      await tester.pumpAndSettle();

      // UI displays customer-friendly 'Vehicle Assistance'
      expect(find.text('Vehicle Assistance'), findsWidgets);

      final submitButton = find.text('Submit Request');
      await tester.ensureVisible(submitButton);
      await tester.tap(submitButton);
      await tester.pumpAndSettle();

      // Transport submits canonical 'Vehicle Repair'
      expect(mockService.lastCreateDto?.categoryHint, 'Vehicle Repair');
      expect(mockService.lastCreateDto?.description, 'Car engine stalling while idling at traffic lights');
    });

    testWidgets('submits canonical categoryHint for Electrical', (tester) async {
      await tester.pumpWidget(
        buildApp(const CreateServiceRequestScreen(initialCategoryPreference: 'Electrical')),
      );
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Problem Description'),
        'Short circuit tripped all main breaker switches',
      );
      await tester.tap(find.text('Next: Location'));
      await tester.pumpAndSettle();
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Location / Address'),
        'Colombo 07',
      );
      await tester.tap(find.text('Next: Review'));
      await tester.pumpAndSettle();

      final submitBtn = find.text('Submit Request');
      await tester.ensureVisible(submitBtn);
      await tester.tap(submitBtn);
      await tester.pumpAndSettle();

      expect(mockService.lastCreateDto?.categoryHint, 'Electrical');
    });

    testWidgets('submits canonical categoryHint for Appliance Repair', (tester) async {
      await tester.pumpWidget(
        buildApp(const CreateServiceRequestScreen(initialCategoryPreference: 'Appliance Repair')),
      );
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Problem Description'),
        'Refrigerator compressor not turning on properly',
      );
      await tester.tap(find.text('Next: Location'));
      await tester.pumpAndSettle();
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Location / Address'),
        'Colombo 04',
      );
      await tester.tap(find.text('Next: Review'));
      await tester.pumpAndSettle();

      final submitBtn2 = find.text('Submit Request');
      await tester.ensureVisible(submitBtn2);
      await tester.tap(submitBtn2);
      await tester.pumpAndSettle();

      expect(mockService.lastCreateDto?.categoryHint, 'Appliance Repair');
    });

    testWidgets('submits null categoryHint when Let AssistLK AI identify is chosen', (tester) async {
      await tester.pumpWidget(
        buildApp(const CreateServiceRequestScreen(initialCategoryPreference: null)),
      );
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Problem Description'),
        'Strange buzzing sound in the wall that happens at night',
      );
      await tester.tap(find.text('Next: Location'));
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Location / Address'),
        'Rajagiriya',
      );
      await tester.tap(find.text('Next: Review'));
      await tester.pumpAndSettle();

      expect(find.text('Let AssistLK AI identify'), findsWidgets);

      final submitButton = find.text('Submit Request');
      await tester.ensureVisible(submitButton);
      await tester.tap(submitButton);
      await tester.pumpAndSettle();

      expect(mockService.lastCreateDto?.categoryHint, isNull);
    });
  });

  group('EditServiceRequestScreen CategoryHint Integration', () {
    testWidgets('pre-fills existing categoryHint in UI', (tester) async {
      final sample = ServiceRequestModel(
        serviceRequestId: 'req-edit-hint',
        customerId: 'cust-1',
        category: 'General',
        categoryHint: 'Vehicle Repair',
        description: 'Brakes making squealing sound',
        locationText: 'Nugegoda',
        urgency: ServiceRequestUrgency.low,
        status: ServiceRequestStatus.created,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.mockRequests = [sample];

      await tester.pumpWidget(buildApp(EditServiceRequestScreen(request: sample)));
      await tester.pumpAndSettle();

      // Vehicle Repair displays as friendly 'Vehicle Assistance'
      expect(find.text('Vehicle Assistance'), findsOneWidget);
    });

    testWidgets('preserves existing categoryHint when only description is modified', (tester) async {
      final sample = ServiceRequestModel(
        serviceRequestId: 'req-edit-desc',
        customerId: 'cust-1',
        category: 'General',
        categoryHint: 'Plumbing',
        description: 'Initial description',
        locationText: 'Kandy',
        urgency: ServiceRequestUrgency.low,
        status: ServiceRequestStatus.created,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.mockRequests = [sample];

      await tester.pumpWidget(buildApp(EditServiceRequestScreen(request: sample)));
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Problem Description'),
        'Updated description for plumbing problem',
      );
      await tester.tap(find.text('Save Changes'));
      await tester.pumpAndSettle();

      expect(mockService.lastUpdateDto?.categoryHint, 'Plumbing');
      expect(mockService.lastUpdateDto?.description, 'Updated description for plumbing problem');
    });

    testWidgets('preserves existing categoryHint when only location is modified', (tester) async {
      final sample = ServiceRequestModel(
        serviceRequestId: 'req-edit-loc',
        customerId: 'cust-1',
        category: 'General',
        categoryHint: 'Electrical',
        description: 'Same description',
        locationText: 'Old Location',
        urgency: ServiceRequestUrgency.low,
        status: ServiceRequestStatus.created,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.mockRequests = [sample];

      await tester.pumpWidget(buildApp(EditServiceRequestScreen(request: sample)));
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Location / Address'),
        'New Address Colombo 03',
      );
      await tester.tap(find.text('Save Changes'));
      await tester.pumpAndSettle();

      expect(mockService.lastUpdateDto?.categoryHint, 'Electrical');
      expect(mockService.lastUpdateDto?.locationText, 'New Address Colombo 03');
    });

    testWidgets('updates categoryHint when customer chooses a different preference', (tester) async {
      final sample = ServiceRequestModel(
        serviceRequestId: 'req-edit-change',
        customerId: 'cust-1',
        category: 'General',
        categoryHint: 'Plumbing',
        description: 'Some problem',
        locationText: 'Colombo',
        urgency: ServiceRequestUrgency.low,
        status: ServiceRequestStatus.created,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.mockRequests = [sample];

      await tester.pumpWidget(buildApp(EditServiceRequestScreen(request: sample)));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Change'));
      await tester.pumpAndSettle();

      // Pick Electrical
      await tester.tap(find.text('Electrical'));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Save Changes'));
      await tester.pumpAndSettle();

      expect(mockService.lastUpdateDto?.categoryHint, 'Electrical');
    });

    testWidgets('updates categoryHint to canonical Vehicle Repair when customer chooses Vehicle Assistance', (tester) async {
      final sample = ServiceRequestModel(
        serviceRequestId: 'req-edit-va',
        customerId: 'cust-1',
        category: 'General',
        categoryHint: 'Plumbing',
        description: 'Some problem',
        locationText: 'Colombo',
        urgency: ServiceRequestUrgency.low,
        status: ServiceRequestStatus.created,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.mockRequests = [sample];

      await tester.pumpWidget(buildApp(EditServiceRequestScreen(request: sample)));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Change'));
      await tester.pumpAndSettle();

      // Pick Vehicle Assistance
      await tester.tap(find.text('Vehicle Assistance'));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Save Changes'));
      await tester.pumpAndSettle();

      expect(mockService.lastUpdateDto?.categoryHint, 'Vehicle Repair');
    });

    testWidgets('clears categoryHint with explicit null when customer chooses Let AssistLK AI identify', (tester) async {
      final sample = ServiceRequestModel(
        serviceRequestId: 'req-edit-null',
        customerId: 'cust-1',
        category: 'General',
        categoryHint: 'Plumbing',
        description: 'Some problem',
        locationText: 'Colombo',
        urgency: ServiceRequestUrgency.low,
        status: ServiceRequestStatus.created,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.mockRequests = [sample];

      await tester.pumpWidget(buildApp(EditServiceRequestScreen(request: sample)));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Change'));
      await tester.pumpAndSettle();

      // Pick Let AssistLK AI identify
      await tester.tap(find.text('Let AssistLK AI identify'));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Save Changes'));
      await tester.pumpAndSettle();

      expect(mockService.lastUpdateDto?.categoryHint, isNull);
    });
  });

  group('ServiceRequestDetailScreen Preference and Lifecycle AI Classification', () {
    testWidgets('shows Service preference and lifecycle wording in Created status', (tester) async {
      final sample = ServiceRequestModel(
        serviceRequestId: 'req-detail-c',
        customerId: 'cust-1',
        category: 'Unclassified',
        categoryHint: 'Plumbing',
        description: 'Bathroom pipe leak',
        locationText: 'Colombo',
        urgency: ServiceRequestUrgency.low,
        status: ServiceRequestStatus.created,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.mockRequests = [sample];

      await tester.pumpWidget(buildApp(const ServiceRequestDetailScreen(requestId: 'req-detail-c')));
      await tester.pumpAndSettle();

      expect(find.text('Service preference: '), findsOneWidget);
      expect(find.text('Plumbing'), findsOneWidget);
      expect(find.text('AssistLK AI classification: '), findsOneWidget);
      expect(find.text('Not analyzed yet'), findsOneWidget);
    });

    testWidgets('shows Analysis in progress in Analyzing status', (tester) async {
      final sample = ServiceRequestModel(
        serviceRequestId: 'req-detail-a',
        customerId: 'cust-1',
        category: 'Unclassified',
        categoryHint: 'Electrical',
        description: 'Power cut',
        locationText: 'Colombo',
        urgency: ServiceRequestUrgency.medium,
        status: ServiceRequestStatus.analyzing,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.mockRequests = [sample];

      await tester.pumpWidget(buildApp(const ServiceRequestDetailScreen(requestId: 'req-detail-a')));
      await tester.pump();
      await tester.pump();

      expect(find.text('Service preference: '), findsOneWidget);
      expect(find.text('Electrical'), findsOneWidget);
      expect(find.text('AssistLK AI classification: '), findsOneWidget);
      expect(find.text('Analysis in progress'), findsOneWidget);
    });

    testWidgets('shows Needs more information in AwaitingInformation status', (tester) async {
      final sample = ServiceRequestModel(
        serviceRequestId: 'req-detail-ai',
        customerId: 'cust-1',
        category: 'Unclassified',
        categoryHint: 'Appliance Repair',
        description: 'Strange hum',
        locationText: 'Colombo',
        urgency: ServiceRequestUrgency.medium,
        status: ServiceRequestStatus.awaitingInformation,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.mockRequests = [sample];

      await tester.pumpWidget(buildApp(const ServiceRequestDetailScreen(requestId: 'req-detail-ai')));
      await tester.pumpAndSettle();

      expect(find.text('Service preference: '), findsOneWidget);
      expect(find.text('Appliance Repair'), findsOneWidget);
      expect(find.text('AssistLK AI classification: '), findsOneWidget);
      expect(find.text('Needs more information'), findsOneWidget);
    });

    testWidgets('ReadyForMatching preserves AnalysisResultCard with completed AssistLK AI classification, handoff section, and no duplicate header rows', (tester) async {
      final sample = ServiceRequestModel(
        serviceRequestId: 'req-detail-rfm',
        customerId: 'cust-1',
        category: 'Electrical',
        categoryHint: 'Plumbing',
        description: 'Fixed issue',
        locationText: 'Colombo',
        urgency: ServiceRequestUrgency.high,
        status: ServiceRequestStatus.readyForMatching,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.mockRequests = [sample];

      await tester.pumpWidget(buildApp(const ServiceRequestDetailScreen(requestId: 'req-detail-rfm')));
      await tester.pumpAndSettle();

      // Header Card does NOT duplicate 'Service preference: ' or 'AssistLK AI classification: '
      expect(find.text('Service preference: '), findsNothing);
      expect(find.text('AssistLK AI classification: '), findsNothing);

      // AnalysisResultCard is preserved as primary AI result/comparison UI
      expect(find.byType(AnalysisResultCard), findsOneWidget);
      expect(find.text('Your preference:'), findsOneWidget);
      expect(find.text('Plumbing'), findsOneWidget);
      expect(find.text('AssistLK AI classification:'), findsOneWidget);
      expect(find.text('Electrical'), findsWidgets);

      // Differing hint/category in ReadyForMatching shows neutral mismatch banner
      expect(find.text('AssistLK AI identified a different service category based on your problem description.'), findsOneWidget);

      // ReadyForMatchingSection handoff UI is present
      expect(find.byType(ReadyForMatchingSection), findsOneWidget);
      expect(find.text('Request Understood'), findsOneWidget);
      expect(find.text('Ready for provider matching'), findsOneWidget);
    });

    testWidgets('ReadyForMatching with matching hint/category shows no mismatch notice', (tester) async {
      final sample = ServiceRequestModel(
        serviceRequestId: 'req-detail-rfm-match',
        customerId: 'cust-1',
        category: 'Electrical',
        categoryHint: 'Electrical',
        description: 'Fixed breaker',
        locationText: 'Colombo',
        urgency: ServiceRequestUrgency.high,
        status: ServiceRequestStatus.readyForMatching,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.mockRequests = [sample];

      await tester.pumpWidget(buildApp(const ServiceRequestDetailScreen(requestId: 'req-detail-rfm-match')));
      await tester.pumpAndSettle();

      expect(find.byType(AnalysisResultCard), findsOneWidget);
      expect(find.text('Your preference:'), findsOneWidget);
      expect(find.text('AssistLK AI classification:'), findsOneWidget);
      expect(find.text('AssistLK AI identified a different service category based on your problem description.'), findsNothing);
      expect(find.byType(ReadyForMatchingSection), findsOneWidget);
    });

    testWidgets('transition from Analyzed to ReadyForMatching does not cause analysis result to disappear', (tester) async {
      final sample = ServiceRequestModel(
        serviceRequestId: 'req-transition',
        customerId: 'cust-1',
        category: 'Plumbing',
        categoryHint: 'Electrical',
        description: 'Pipe leaking in bathroom',
        locationText: 'Kandy',
        urgency: ServiceRequestUrgency.medium,
        status: ServiceRequestStatus.analyzed,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.mockRequests = [sample];

      await tester.pumpWidget(buildApp(const ServiceRequestDetailScreen(requestId: 'req-transition')));
      await tester.pumpAndSettle();

      // In Analyzed status: AnalysisResultCard is shown with mismatch notice, Mark Ready button is present
      expect(find.byType(AnalysisResultCard), findsOneWidget);
      expect(find.text('Your preference:'), findsOneWidget);
      expect(find.text('Electrical'), findsOneWidget);
      expect(find.text('AssistLK AI classification:'), findsOneWidget);
      expect(find.text('Plumbing'), findsWidgets);
      expect(find.text('AssistLK AI identified a different service category based on your problem description.'), findsOneWidget);
      expect(find.text('Mark Ready for Matching'), findsOneWidget);
      expect(find.byType(ReadyForMatchingSection), findsNothing);

      // Perform transition
      await tester.ensureVisible(find.text('Mark Ready for Matching'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Mark Ready for Matching'));
      await tester.pumpAndSettle();

      // Post-transition: AnalysisResultCard REMAINS present and ReadyForMatchingSection is added
      expect(find.byType(AnalysisResultCard), findsOneWidget);
      expect(find.text('Your preference:'), findsOneWidget);
      expect(find.text('Electrical'), findsOneWidget);
      expect(find.text('AssistLK AI classification:'), findsOneWidget);
      expect(find.text('Plumbing'), findsWidgets);
      expect(find.text('AssistLK AI identified a different service category based on your problem description.'), findsOneWidget);
      expect(find.byType(ReadyForMatchingSection), findsOneWidget);
      expect(find.text('Mark Ready for Matching'), findsNothing);
    });

    testWidgets('in Analyzed status, avoids duplicate rows in Header Card and delegates to AnalysisResultCard', (tester) async {
      final sample = ServiceRequestModel(
        serviceRequestId: 'req-detail-analyzed',
        customerId: 'cust-1',
        category: 'Electrical',
        categoryHint: 'Plumbing',
        description: 'Circuit tripping',
        locationText: 'Colombo',
        urgency: ServiceRequestUrgency.high,
        status: ServiceRequestStatus.analyzed,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.mockRequests = [sample];

      await tester.pumpWidget(buildApp(const ServiceRequestDetailScreen(requestId: 'req-detail-analyzed')));
      await tester.pumpAndSettle();

      // Header Card does NOT duplicate 'Service preference: '
      expect(find.text('Service preference: '), findsNothing);

      // AnalysisResultCard is the primary place
      expect(find.byType(AnalysisResultCard), findsOneWidget);
      expect(find.text('Your preference:'), findsOneWidget);
      expect(find.text('AssistLK AI classification:'), findsOneWidget);
      expect(find.text('Plumbing'), findsOneWidget);
      expect(find.text('Electrical'), findsWidgets);
    });
  });

  group('AnalysisResultCard Mismatch Display Safety', () {
    testWidgets('matching category and hint shows no mismatch banner in ReadyForMatching status', (tester) async {
      final analysis = ProblemUnderstandingResultModel(
        workflowId: 'wf-rfm-1',
        executionId: 'ex-rfm-1',
        serviceRequestId: 'req-rfm-1',
        status: ServiceRequestStatus.readyForMatching,
        category: 'Plumbing',
        problemSummary: 'Leaking pipe under kitchen sink',
        urgency: ServiceRequestUrgency.medium,
        confidence: 0.95,
        needsMoreInformation: false,
        followUpQuestions: const [],
      );

      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.lightTheme,
          home: Scaffold(
            body: AnalysisResultCard(
              analysis: analysis,
              categoryHint: 'Plumbing',
              status: ServiceRequestStatus.readyForMatching,
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Your preference:'), findsOneWidget);
      expect(find.text('Plumbing'), findsNWidgets(2));
      expect(find.text('AssistLK AI identified a different service category based on your problem description.'), findsNothing);
    });

    testWidgets('mismatching category and hint in ReadyForMatching status shows neutral mismatch banner', (tester) async {
      final analysis = ProblemUnderstandingResultModel(
        workflowId: 'wf-rfm-2',
        executionId: 'ex-rfm-2',
        serviceRequestId: 'req-rfm-2',
        status: ServiceRequestStatus.readyForMatching,
        category: 'Plumbing',
        problemSummary: 'Water pipe leak',
        urgency: ServiceRequestUrgency.medium,
        confidence: 0.92,
        needsMoreInformation: false,
        followUpQuestions: const [],
      );

      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.lightTheme,
          home: Scaffold(
            body: AnalysisResultCard(
              analysis: analysis,
              categoryHint: 'Electrical',
              status: ServiceRequestStatus.readyForMatching,
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Your preference:'), findsOneWidget);
      expect(find.text('Electrical'), findsOneWidget);
      expect(find.text('AssistLK AI classification:'), findsOneWidget);
      expect(find.text('Plumbing'), findsOneWidget);
      expect(find.text('AssistLK AI identified a different service category based on your problem description.'), findsOneWidget);
      expect(find.byIcon(Icons.info_outline_rounded), findsOneWidget);
    });
    testWidgets('matching category and hint shows no mismatch banner in Analyzed status', (tester) async {
      final analysis = ProblemUnderstandingResultModel(
        workflowId: 'wf-1',
        executionId: 'ex-1',
        serviceRequestId: 'req-1',
        status: ServiceRequestStatus.analyzed,
        category: 'Plumbing',
        problemSummary: 'Leaking pipe under kitchen sink',
        urgency: ServiceRequestUrgency.medium,
        confidence: 0.95,
        needsMoreInformation: false,
        followUpQuestions: const [],
      );

      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.lightTheme,
          home: Scaffold(
            body: AnalysisResultCard(
              analysis: analysis,
              categoryHint: 'Plumbing',
              status: ServiceRequestStatus.analyzed,
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Your preference:'), findsOneWidget);
      expect(find.text('Plumbing'), findsNWidgets(2)); // in preference and AI classification
      expect(find.text('AssistLK AI identified a different service category based on your problem description.'), findsNothing);
    });

    testWidgets('mismatching category and hint in Analyzed status shows neutral mismatch banner', (tester) async {
      final analysis = ProblemUnderstandingResultModel(
        workflowId: 'wf-2',
        executionId: 'ex-2',
        serviceRequestId: 'req-2',
        status: ServiceRequestStatus.analyzed,
        category: 'Plumbing',
        problemSummary: 'Water pipe leak',
        urgency: ServiceRequestUrgency.medium,
        confidence: 0.92,
        needsMoreInformation: false,
        followUpQuestions: const [],
      );

      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.lightTheme,
          home: Scaffold(
            body: AnalysisResultCard(
              analysis: analysis,
              categoryHint: 'Electrical',
              status: ServiceRequestStatus.analyzed,
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Your preference:'), findsOneWidget);
      expect(find.text('Electrical'), findsOneWidget);
      expect(find.text('AssistLK AI classification:'), findsOneWidget);
      expect(find.text('Plumbing'), findsOneWidget);
      expect(find.text('AssistLK AI identified a different service category based on your problem description.'), findsOneWidget);
      expect(find.byIcon(Icons.info_outline_rounded), findsOneWidget);
    });

    testWidgets('canonical equivalence between Vehicle Assistance and Vehicle Repair produces no mismatch', (tester) async {
      final analysis = ProblemUnderstandingResultModel(
        workflowId: 'wf-3',
        executionId: 'ex-3',
        serviceRequestId: 'req-3',
        status: ServiceRequestStatus.analyzed,
        category: 'Vehicle Repair',
        problemSummary: 'Transmission failure',
        urgency: ServiceRequestUrgency.high,
        confidence: 0.94,
        needsMoreInformation: false,
        followUpQuestions: const [],
      );

      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.lightTheme,
          home: Scaffold(
            body: AnalysisResultCard(
              analysis: analysis,
              categoryHint: 'Vehicle Assistance',
              status: ServiceRequestStatus.analyzed,
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Your preference:'), findsOneWidget);
      expect(find.text('Vehicle Assistance'), findsOneWidget);
      expect(find.text('AssistLK AI classification:'), findsOneWidget);
      expect(find.text('Vehicle Repair'), findsOneWidget);
      expect(find.text('AssistLK AI identified a different service category based on your problem description.'), findsNothing);
    });

    testWidgets('null categoryHint (Let AssistLK AI identify) shows no mismatch banner', (tester) async {
      final analysis = ProblemUnderstandingResultModel(
        workflowId: 'wf-4',
        executionId: 'ex-4',
        serviceRequestId: 'req-4',
        status: ServiceRequestStatus.analyzed,
        category: 'Electrical',
        problemSummary: 'Tripping circuit',
        urgency: ServiceRequestUrgency.high,
        confidence: 0.91,
        needsMoreInformation: false,
        followUpQuestions: const [],
      );

      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.lightTheme,
          home: Scaffold(
            body: AnalysisResultCard(
              analysis: analysis,
              categoryHint: null,
              status: ServiceRequestStatus.analyzed,
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Your preference:'), findsOneWidget);
      expect(find.text('Let AssistLK AI identify'), findsOneWidget);
      expect(find.text('AssistLK AI identified a different service category based on your problem description.'), findsNothing);
    });

    testWidgets('Unclassified AI category shows no mismatch banner', (tester) async {
      final analysis = ProblemUnderstandingResultModel(
        workflowId: 'wf-5',
        executionId: 'ex-5',
        serviceRequestId: 'req-5',
        status: ServiceRequestStatus.analyzed,
        category: 'Unclassified',
        problemSummary: 'Need help',
        urgency: ServiceRequestUrgency.low,
        confidence: 0.50,
        needsMoreInformation: true,
        followUpQuestions: const [],
      );

      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.lightTheme,
          home: Scaffold(
            body: AnalysisResultCard(
              analysis: analysis,
              categoryHint: 'Plumbing',
              status: ServiceRequestStatus.analyzed,
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('AssistLK AI identified a different service category based on your problem description.'), findsNothing);
    });

    testWidgets('lifecycle safety: mismatch notice suppressed when status is Created, Analyzing, AwaitingInformation, Cancelled', (tester) async {
      final analysis = ProblemUnderstandingResultModel(
        workflowId: 'wf-6',
        executionId: 'ex-6',
        serviceRequestId: 'req-6',
        status: ServiceRequestStatus.awaitingInformation,
        category: 'Plumbing',
        problemSummary: 'Water leak',
        urgency: ServiceRequestUrgency.medium,
        confidence: 0.88,
        needsMoreInformation: true,
        followUpQuestions: const [],
      );

      for (final unsafeStatus in [
        ServiceRequestStatus.created,
        ServiceRequestStatus.analyzing,
        ServiceRequestStatus.awaitingInformation,
        ServiceRequestStatus.cancelled,
      ]) {
        await tester.pumpWidget(
          MaterialApp(
            theme: AppTheme.lightTheme,
            home: Scaffold(
              body: AnalysisResultCard(
                analysis: analysis,
                categoryHint: 'Electrical', // deliberately different
                status: unsafeStatus,
              ),
            ),
          ),
        );
        await tester.pumpAndSettle();

        expect(
          find.text('AssistLK AI identified a different service category based on your problem description.'),
          findsNothing,
          reason: 'Status $unsafeStatus must never show mismatch notice',
        );
      }
    });
  });

  group('Real GPS Service Location Feature 2', () {
    late MockLocationService mockLocationService;

    setUp(() {
      mockLocationService = MockLocationService();
    });

    testWidgets('1. Manual location flow works without GPS (coordinates remain null, zero GPS calls)',
        (tester) async {
      await tester.pumpWidget(
        buildApp(CreateServiceRequestScreen(locationService: mockLocationService)),
      );
      await tester.pumpAndSettle();

      // Step 1: Description
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Problem Description'),
        'Major ceiling leak in the living room after heavy rain',
      );
      await tester.tap(find.text('Next: Location'));
      await tester.pumpAndSettle();

      // Step 2: Location (manual only, do not tap GPS)
      expect(find.byKey(const Key('use_current_location_button')), findsOneWidget);
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Location / Address'),
        '123 Galle Road, Colombo 03',
      );
      await tester.tap(find.text('Next: Review'));
      await tester.pumpAndSettle();

      // Step 3: Review & Submit
      expect(find.text('Review Service Request'), findsOneWidget);
      expect(find.text('123 Galle Road, Colombo 03'), findsOneWidget);
      expect(find.textContaining('GPS location captured'), findsNothing);

      final submitBtn = find.text('Submit Request');
      await tester.ensureVisible(submitBtn);
      await tester.tap(submitBtn);
      await tester.pumpAndSettle();

      expect(mockService.lastCreateDto, isNotNull);
      expect(mockService.lastCreateDto!.locationText, '123 Galle Road, Colombo 03');
      expect(mockService.lastCreateDto!.latitude, isNull);
      expect(mockService.lastCreateDto!.longitude, isNull);
      expect(mockLocationService.getCurrentLocationCallCount, 0);
    });

    testWidgets('2. GPS success populates latitude and longitude into DTO and displays in review',
        (tester) async {
      await tester.pumpWidget(
        buildApp(CreateServiceRequestScreen(locationService: mockLocationService)),
      );
      await tester.pumpAndSettle();

      // Step 1: Description
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Problem Description'),
        'Water leaking under bathroom sink pipe heavily',
      );
      await tester.tap(find.text('Next: Location'));
      await tester.pumpAndSettle();

      // Step 2: Location -> Tap Use Current Location
      await tester.tap(find.byKey(const Key('use_current_location_button')));
      await tester.pumpAndSettle();

      expect(find.textContaining('GPS location captured (6.9271, 79.8612)'), findsOneWidget);
      expect(mockLocationService.getCurrentLocationCallCount, 1);

      // Provide human-readable address
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Location / Address'),
        'Colombo 03, Havelock Road',
      );
      await tester.tap(find.text('Next: Review'));
      await tester.pumpAndSettle();

      // Step 3: Review displays GPS location captured
      expect(find.text('Review Service Request'), findsOneWidget);
      expect(find.text('Colombo 03, Havelock Road'), findsOneWidget);
      expect(find.textContaining('GPS location captured (6.9271, 79.8612)'), findsOneWidget);

      final submitBtn = find.text('Submit Request');
      await tester.ensureVisible(submitBtn);
      await tester.tap(submitBtn);
      await tester.pumpAndSettle();

      expect(mockService.lastCreateDto, isNotNull);
      expect(mockService.lastCreateDto!.locationText, 'Colombo 03, Havelock Road');
      expect(mockService.lastCreateDto!.latitude, 6.9271);
      expect(mockService.lastCreateDto!.longitude, 79.8612);
    });

    testWidgets('3. Remove GPS button clears captured coordinates', (tester) async {
      await tester.pumpWidget(
        buildApp(CreateServiceRequestScreen(locationService: mockLocationService)),
      );
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Problem Description'),
        'Water leaking under bathroom sink pipe heavily',
      );
      await tester.tap(find.text('Next: Location'));
      await tester.pumpAndSettle();

      // Capture GPS
      await tester.tap(find.byKey(const Key('use_current_location_button')));
      await tester.pumpAndSettle();
      expect(find.textContaining('GPS location captured (6.9271, 79.8612)'), findsOneWidget);

      // Remove GPS
      await tester.tap(find.text('Remove GPS'));
      await tester.pumpAndSettle();

      expect(find.textContaining('GPS location captured'), findsNothing);

      // Enter manual address & submit
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Location / Address'),
        'Manual address without GPS',
      );
      await tester.tap(find.text('Next: Review'));
      await tester.pumpAndSettle();

      expect(find.textContaining('GPS location captured'), findsNothing);

      final submitBtn = find.text('Submit Request');
      await tester.ensureVisible(submitBtn);
      await tester.tap(submitBtn);
      await tester.pumpAndSettle();

      expect(mockService.lastCreateDto!.latitude, isNull);
      expect(mockService.lastCreateDto!.longitude, isNull);
    });

    testWidgets('4. Permission denied shows explanation and allows manual fallback',
        (tester) async {
      mockLocationService.permissionStatus = LocationAccessStatus.denied;

      await tester.pumpWidget(
        buildApp(CreateServiceRequestScreen(locationService: mockLocationService)),
      );
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Problem Description'),
        'Water leaking under bathroom sink pipe heavily',
      );
      await tester.tap(find.text('Next: Location'));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('use_current_location_button')));
      await tester.pumpAndSettle();

      expect(
        find.text('Location permission was denied. You can enter the service location manually.'),
        findsOneWidget,
      );

      // Manual fallback works
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Location / Address'),
        '45 Havelock Road, Colombo 05',
      );
      await tester.tap(find.text('Next: Review'));
      await tester.pumpAndSettle();

      final submitBtn = find.text('Submit Request');
      await tester.ensureVisible(submitBtn);
      await tester.tap(submitBtn);
      await tester.pumpAndSettle();

      expect(mockService.lastCreateDto!.latitude, isNull);
      expect(mockService.lastCreateDto!.longitude, isNull);
    });

    testWidgets('5. Permission permanently denied shows settings guidance and manual fallback',
        (tester) async {
      mockLocationService.permissionStatus = LocationAccessStatus.deniedForever;

      await tester.pumpWidget(
        buildApp(CreateServiceRequestScreen(locationService: mockLocationService)),
      );
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Problem Description'),
        'Main switchboard power trip continuously',
      );
      await tester.tap(find.text('Next: Location'));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('use_current_location_button')));
      await tester.pumpAndSettle();

      expect(
        find.text(
          'Location access is disabled for AssistLK. Enable it in device settings or enter the location manually.',
        ),
        findsOneWidget,
      );
    });

    testWidgets('6. Location services disabled shows turn-on guidance and manual fallback',
        (tester) async {
      mockLocationService.serviceEnabled = false;

      await tester.pumpWidget(
        buildApp(CreateServiceRequestScreen(locationService: mockLocationService)),
      );
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Problem Description'),
        'Main switchboard power trip continuously',
      );
      await tester.tap(find.text('Next: Location'));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('use_current_location_button')));
      await tester.pumpAndSettle();

      expect(
        find.text('Location services are turned off. You can enter the address manually.'),
        findsOneWidget,
      );
    });

    testWidgets('7. GPS timeout shows timeout message without blocking manual entry',
        (tester) async {
      mockLocationService.customResult = const LocationResult(
        status: LocationAccessStatus.timeout,
        message:
            'Could not get your current location in time. Please enter the location manually.',
      );

      await tester.pumpWidget(
        buildApp(CreateServiceRequestScreen(locationService: mockLocationService)),
      );
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Problem Description'),
        'Main switchboard power trip continuously',
      );
      await tester.tap(find.text('Next: Location'));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('use_current_location_button')));
      await tester.pumpAndSettle();

      expect(
        find.text('Could not get your current location in time. Please enter the location manually.'),
        findsOneWidget,
      );
    });

    testWidgets('8. GPS exception handled safely without crashing UI', (tester) async {
      mockLocationService.shouldThrow = true;

      await tester.pumpWidget(
        buildApp(CreateServiceRequestScreen(locationService: mockLocationService)),
      );
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Problem Description'),
        'Main switchboard power trip continuously',
      );
      await tester.tap(find.text('Next: Location'));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('use_current_location_button')));
      await tester.pumpAndSettle();

      expect(
        find.text('Could not retrieve your current location. Please enter the location manually.'),
        findsOneWidget,
      );
    });

    testWidgets('9. CategoryHint and GPS coordinates submitted together', (tester) async {
      await tester.pumpWidget(
        buildApp(
          CreateServiceRequestScreen(
            initialCategoryPreference: 'Vehicle Assistance',
            locationService: mockLocationService,
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Problem Description'),
        'Vehicle radiator overheating on roadside',
      );
      await tester.tap(find.text('Next: Location'));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('use_current_location_button')));
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Location / Address'),
        'Baseline Road, Dematagoda',
      );
      await tester.tap(find.text('Next: Review'));
      await tester.pumpAndSettle();

      final submitBtn = find.text('Submit Request');
      await tester.ensureVisible(submitBtn);
      await tester.tap(submitBtn);
      await tester.pumpAndSettle();

      expect(mockService.lastCreateDto!.categoryHint, 'Vehicle Repair');
      expect(mockService.lastCreateDto!.latitude, 6.9271);
      expect(mockService.lastCreateDto!.longitude, 79.8612);
    });

    testWidgets('10. EditServiceRequestScreen preserves coordinates when locationText is unchanged',
        (tester) async {
      final existingReq = ServiceRequestModel(
        serviceRequestId: 'req-edit-1',
        customerId: 'cust-1',
        category: 'Plumbing',
        categoryHint: 'Plumbing',
        description: 'Original description',
        locationText: '123 Galle Road, Colombo 03',
        latitude: 6.9000,
        longitude: 79.8500,
        urgency: ServiceRequestUrgency.low,
        status: ServiceRequestStatus.created,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.mockRequests = [existingReq];

      await tester.pumpWidget(
        buildApp(EditServiceRequestScreen(
          request: existingReq,
          locationService: mockLocationService,
        )),
      );
      await tester.pumpAndSettle();

      // Only change description
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Problem Description'),
        'Updated description for existing pipe issue',
      );

      final saveBtn = find.text('Save Changes');
      await tester.ensureVisible(saveBtn);
      await tester.tap(saveBtn);
      await tester.pumpAndSettle();

      expect(mockService.lastUpdateDto, isNotNull);
      expect(mockService.lastUpdateDto!.description, 'Updated description for existing pipe issue');
      expect(mockService.lastUpdateDto!.locationText, '123 Galle Road, Colombo 03');
      expect(mockService.lastUpdateDto!.latitude, 6.9000);
      expect(mockService.lastUpdateDto!.longitude, 79.8500);
    });

    testWidgets('11. EditServiceRequestScreen blocks PUT and shows confirmation when locationText changes with GPS',
        (tester) async {
      final existingReq = ServiceRequestModel(
        serviceRequestId: 'req-edit-2',
        customerId: 'cust-1',
        category: 'Plumbing',
        description: 'Original description',
        locationText: 'Colombo 03',
        latitude: 6.9000,
        longitude: 79.8500,
        urgency: ServiceRequestUrgency.low,
        status: ServiceRequestStatus.created,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.mockRequests = [existingReq];
      mockService.lastUpdateDto = null;

      await tester.pumpWidget(
        buildApp(EditServiceRequestScreen(
          request: existingReq,
          locationService: mockLocationService,
        )),
      );
      await tester.pumpAndSettle();

      // Edit location address to Kandy
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Location / Address'),
        'Kandy City Center',
      );
      await tester.pumpAndSettle();

      // Confirmation prompt appears in UI
      expect(find.byKey(const Key('edit_gps_confirmation_prompt')), findsOneWidget);

      // Attempt to save without choosing
      final saveBtn = find.text('Save Changes');
      await tester.ensureVisible(saveBtn);
      await tester.tap(saveBtn);
      await tester.pumpAndSettle();

      // PUT was blocked!
      expect(mockService.lastUpdateDto, isNull);
      expect(
        find.text('Please confirm how to handle the attached GPS coordinates.'),
        findsOneWidget,
      );
    });

    testWidgets('12. EditServiceRequestScreen allows keeping existing coordinates on confirmation',
        (tester) async {
      final existingReq = ServiceRequestModel(
        serviceRequestId: 'req-edit-3',
        customerId: 'cust-1',
        category: 'Plumbing',
        description: 'Original description',
        locationText: 'Colombo 03',
        latitude: 6.9000,
        longitude: 79.8500,
        urgency: ServiceRequestUrgency.low,
        status: ServiceRequestStatus.created,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.mockRequests = [existingReq];

      await tester.pumpWidget(
        buildApp(EditServiceRequestScreen(
          request: existingReq,
          locationService: mockLocationService,
        )),
      );
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Location / Address'),
        'Colombo 03, Apartment 4B',
      );
      await tester.pumpAndSettle();

      // Explicitly choose: Keep Existing GPS Coordinates
      final keepBtn = find.byKey(const Key('edit_keep_gps_button'));
      await tester.ensureVisible(keepBtn);
      await tester.tap(keepBtn);
      await tester.pumpAndSettle();

      final saveBtn = find.text('Save Changes');
      await tester.ensureVisible(saveBtn);
      await tester.tap(saveBtn);
      await tester.pumpAndSettle();

      expect(mockService.lastUpdateDto, isNotNull);
      expect(mockService.lastUpdateDto!.locationText, 'Colombo 03, Apartment 4B');
      expect(mockService.lastUpdateDto!.latitude, 6.9000);
      expect(mockService.lastUpdateDto!.longitude, 79.8500);
    });

    testWidgets('13. EditServiceRequestScreen allows removing GPS coordinates on confirmation',
        (tester) async {
      final existingReq = ServiceRequestModel(
        serviceRequestId: 'req-edit-4',
        customerId: 'cust-1',
        category: 'Plumbing',
        description: 'Original description',
        locationText: 'Colombo 03',
        latitude: 6.9000,
        longitude: 79.8500,
        urgency: ServiceRequestUrgency.low,
        status: ServiceRequestStatus.created,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.mockRequests = [existingReq];

      await tester.pumpWidget(
        buildApp(EditServiceRequestScreen(
          request: existingReq,
          locationService: mockLocationService,
        )),
      );
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Location / Address'),
        'Galle Fort',
      );
      await tester.pumpAndSettle();

      // Explicitly choose: Remove GPS Coordinates
      final removeBtn = find.byKey(const Key('edit_remove_gps_button'));
      await tester.ensureVisible(removeBtn);
      await tester.tap(removeBtn);
      await tester.pumpAndSettle();

      final saveBtn = find.text('Save Changes');
      await tester.ensureVisible(saveBtn);
      await tester.tap(saveBtn);
      await tester.pumpAndSettle();

      expect(mockService.lastUpdateDto, isNotNull);
      expect(mockService.lastUpdateDto!.locationText, 'Galle Fort');
      expect(mockService.lastUpdateDto!.latitude, isNull);
      expect(mockService.lastUpdateDto!.longitude, isNull);
    });

    testWidgets('14. EditServiceRequestScreen allows updating with current location on confirmation',
        (tester) async {
      final existingReq = ServiceRequestModel(
        serviceRequestId: 'req-edit-5',
        customerId: 'cust-1',
        category: 'Plumbing',
        description: 'Original description',
        locationText: 'Colombo 03',
        latitude: 6.9000,
        longitude: 79.8500,
        urgency: ServiceRequestUrgency.low,
        status: ServiceRequestStatus.created,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.mockRequests = [existingReq];

      await tester.pumpWidget(
        buildApp(EditServiceRequestScreen(
          request: existingReq,
          locationService: mockLocationService,
        )),
      );
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Location / Address'),
        'New Street Location',
      );
      await tester.pumpAndSettle();

      // Explicitly choose: Update with Current Location
      final updateBtn = find.byKey(const Key('edit_update_gps_button'));
      await tester.ensureVisible(updateBtn);
      await tester.tap(updateBtn);
      await tester.pumpAndSettle();

      final saveBtn = find.text('Save Changes');
      await tester.ensureVisible(saveBtn);
      await tester.tap(saveBtn);
      await tester.pumpAndSettle();

      expect(mockService.lastUpdateDto, isNotNull);
      expect(mockService.lastUpdateDto!.locationText, 'New Street Location');
      expect(mockService.lastUpdateDto!.latitude, 6.9271);
      expect(mockService.lastUpdateDto!.longitude, 79.8612);
    });

    testWidgets('15. ServiceRequestDetailScreen displays GPS location captured when coordinates are present',
        (tester) async {
      final reqWithCoords = ServiceRequestModel(
        serviceRequestId: 'req-detail-gps',
        customerId: 'cust-1',
        category: 'Plumbing',
        description: 'Water leak',
        locationText: '123 Galle Road, Colombo 03',
        latitude: 6.9271,
        longitude: 79.8612,
        urgency: ServiceRequestUrgency.low,
        status: ServiceRequestStatus.created,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );
      mockService.mockRequests = [reqWithCoords];

      await tester.pumpWidget(
        buildApp(const ServiceRequestDetailScreen(requestId: 'req-detail-gps')),
      );
      await tester.pumpAndSettle();

      expect(find.text('123 Galle Road, Colombo 03'), findsOneWidget);
      expect(find.textContaining('GPS location captured (6.9271, 79.8612)'), findsOneWidget);
    });
  });
}

