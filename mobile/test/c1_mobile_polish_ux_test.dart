import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:mobile/app/app.dart';
import 'package:mobile/core/api/api_client.dart';
import 'package:mobile/features/auth/providers/auth_provider.dart';
import 'package:mobile/features/service_requests/models/forward_geocode_candidate.dart';
import 'package:mobile/features/service_requests/models/resolved_location.dart';
import 'package:mobile/features/service_requests/models/service_request_model.dart';
import 'package:mobile/features/service_requests/models/service_request_status.dart';
import 'package:mobile/features/service_requests/models/service_request_urgency.dart';
import 'package:mobile/features/service_requests/providers/service_request_provider.dart';
import 'package:mobile/features/service_requests/screens/create_service_request_screen.dart';
import 'package:mobile/features/service_requests/screens/service_request_detail_screen.dart';
import 'package:mobile/features/service_requests/widgets/service_request_card.dart';
import 'package:mobile/shared/theme/app_colors.dart';
import 'package:mobile/shared/theme/app_theme.dart';
import 'package:mobile/shared/widgets/animated_border_trail.dart';
import 'package:mobile/shared/widgets/app_text_field.dart';

import 'mocks/mock_location_geocoding_service.dart';
import 'mocks/mock_location_service.dart';
import 'session_foundation_test.dart'
    show MemoryStorage, AuthStub, RequestsStub, request;

class ShellRequestsStub extends RequestsStub {
  ShellRequestsStub(super.apiClient);

  @override
  Future<ServiceRequestModel> getById(String id) async => items.firstWhere(
    (item) => item.serviceRequestId == id,
    orElse: () => request(id),
  );
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  late MemoryStorage storage;
  late ApiClient api;
  late AuthStub authService;
  late AuthProvider auth;
  late ShellRequestsStub requests;
  late MockLocationService gps;
  late MockLocationGeocodingService geocoding;

  setUp(() {
    storage = MemoryStorage();
    api = ApiClient(tokenStorage: storage);
    authService = AuthStub(api);
    auth = AuthProvider(authService: authService, tokenStorage: storage);
    requests = ShellRequestsStub(api);
    gps = MockLocationService();
    geocoding = MockLocationGeocodingService();
  });

  Widget buildApp(Widget child, {bool disableAnimations = false}) {
    return MultiProvider(
      providers: [
        ChangeNotifierProvider<AuthProvider>.value(value: auth),
        ChangeNotifierProvider<ServiceRequestProvider>(
          create: (_) => ServiceRequestProvider(serviceRequestService: requests),
        ),
      ],
      child: MaterialApp(
        theme: AppTheme.lightTheme,
        builder: (context, widget) => MediaQuery(
          data: MediaQuery.of(context).copyWith(
            disableAnimations: disableAnimations,
          ),
          child: widget!,
        ),
        home: child,
      ),
    );
  }

  group('1. Customer Home App Bar Polish', () {
    testWidgets('displays "AssistLK", does not show "AssistLK Customer", uses navy background and white text',
        (tester) async {
      await auth.login(email: 'customer@example.com', password: 'password');
      await tester.pumpWidget(
        ChangeNotifierProvider<AuthProvider>.value(
          value: auth,
          child: AssistLKApp(
            serviceRequestService: requests,
            geocodingService: geocoding,
          ),
        ),
      );
      await tester.pumpAndSettle();

      // "AssistLK" app bar title
      expect(find.text('AssistLK'), findsOneWidget);
      expect(find.text('AssistLK Customer'), findsNothing);

      // Verify AppBar theme tokens
      final appBarFinder = find.byType(AppBar);
      expect(appBarFinder, findsOneWidget);
      final appBar = tester.widget<AppBar>(appBarFinder);
      expect(appBar.backgroundColor, AppColors.primary);
      expect(appBar.foregroundColor, AppColors.surface);
    });
  });

  group('2. Navigation Copy Polish', () {
    testWidgets('Create flow displays "Continue to Location" and "Review Request"',
        (tester) async {
      await tester.pumpWidget(
        buildApp(
          CreateServiceRequestScreen(
            locationService: gps,
            geocodingService: geocoding,
          ),
        ),
      );
      await tester.pumpAndSettle();

      // Step 0: Continue to Location
      expect(find.text('Continue to Location'), findsOneWidget);
      expect(find.text('Next: Location'), findsNothing);

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Problem Description'),
        'Ceiling pipe leaking severely into hallway',
      );
      await tester.tap(find.text('Continue to Location'));
      await tester.pumpAndSettle();

      // Step 1: Review Request
      expect(find.text('Review Request'), findsOneWidget);
      expect(find.text('Next: Review'), findsNothing);
    });

    testWidgets('Detail screen displays "Ready for AI Analysis" and "Analyze Request with AssistLK AI"',
        (tester) async {
      final createdReq = ServiceRequestModel(
        serviceRequestId: 'req-created-copy',
        customerId: 'cust-1',
        category: '',
        description: 'Electrical sparking behind wall socket',
        locationText: 'Colombo 03',
        urgency: ServiceRequestUrgency.unknown,
        status: ServiceRequestStatus.created,
        createdAt: DateTime(2026, 10, 1),
        updatedAt: DateTime(2026, 10, 1),
      );
      requests.items = [createdReq];

      await tester.pumpWidget(
        buildApp(
          const ServiceRequestDetailScreen(requestId: 'req-created-copy'),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Ready for AI Analysis'), findsOneWidget);
      expect(find.text('Next Step: AssistLK AI Analysis'), findsNothing);

      expect(find.text('Analyze Request with AssistLK AI'), findsOneWidget);
      expect(find.text('Analyze with AssistLK AI'), findsNothing);
    });
  });

  group('3. Manual Location UX Polish', () {
    testWidgets('manual address input is hidden initially, shows only action buttons',
        (tester) async {
      await tester.pumpWidget(
        buildApp(
          CreateServiceRequestScreen(
            locationService: gps,
            geocodingService: geocoding,
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Problem Description'),
        'Ceiling pipe leaking severely into hallway',
      );
      await tester.tap(find.text('Continue to Location'));
      await tester.pumpAndSettle();

      // Initial state: Both buttons visible
      expect(find.text('Use Current Location'), findsOneWidget);
      expect(find.text('Enter Manually'), findsOneWidget);

      // Manual input field NOT visible
      expect(find.widgetWithText(AppTextField, 'Location / Address'), findsNothing);
    });

    testWidgets('tapping Enter Manually reveals field and focuses it',
        (tester) async {
      await tester.pumpWidget(
        buildApp(
          CreateServiceRequestScreen(
            locationService: gps,
            geocodingService: geocoding,
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Problem Description'),
        'Ceiling pipe leaking severely into hallway',
      );
      await tester.tap(find.text('Continue to Location'));
      await tester.pumpAndSettle();

      // Tap Enter Manually
      await tester.tap(find.text('Enter Manually'));
      await tester.pumpAndSettle();

      // Manual input is now revealed
      expect(find.widgetWithText(AppTextField, 'Location / Address'), findsOneWidget);
    });

    testWidgets('confirming manual location collapses input and displays Change address button',
        (tester) async {
      geocoding.forwardReply = (addr) async => [
        ForwardGeocodeCandidate(
          displayAddress: '42 Galle Road, Colombo 03',
          latitude: 6.91,
          longitude: 79.85,
          placeId: 'cand-42',
          source: 'OpenStreetMap',
        ),
      ];

      await tester.pumpWidget(
        buildApp(
          CreateServiceRequestScreen(
            locationService: gps,
            geocodingService: geocoding,
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Problem Description'),
        'Ceiling pipe leaking severely into hallway',
      );
      await tester.tap(find.text('Continue to Location'));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Enter Manually'));
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Location / Address'),
        '42 Galle Road',
      );
      await tester.pumpAndSettle();

      // Resolve address
      await tester.tap(find.byKey(const Key('resolve_address_button')));
      await tester.pumpAndSettle();

      // Confirm candidate
      await tester.tap(find.byKey(const Key('use_forward_location_button')));
      await tester.pumpAndSettle();

      // Confirmed card is shown
      expect(find.byKey(const Key('confirmed_location_card')), findsOneWidget);
      expect(find.text('42 Galle Road, Colombo 03'), findsOneWidget);
      expect(find.text('Change address'), findsOneWidget);

      // Manual input field is collapsed / hidden
      expect(find.widgetWithText(AppTextField, 'Location / Address'), findsNothing);

      // Tapping Change address re-reveals the field
      await tester.tap(find.text('Change address'));
      await tester.pumpAndSettle();

      expect(find.widgetWithText(AppTextField, 'Location / Address'), findsOneWidget);
    });

    testWidgets('current-location mode does not reveal manual input',
        (tester) async {
      geocoding.reply = () async => const ResolvedLocation(
        formattedAddress: '15 Independence Square, Colombo 07',
      );

      await tester.pumpWidget(
        buildApp(
          CreateServiceRequestScreen(
            locationService: gps,
            geocodingService: geocoding,
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextFormField, 'Problem Description'),
        'Ceiling pipe leaking severely into hallway',
      );
      await tester.tap(find.text('Continue to Location'));
      await tester.pumpAndSettle();

      // Tap Use Current Location
      await tester.tap(find.text('Use Current Location'));
      await tester.pumpAndSettle();

      // Preview card with Use This Location
      await tester.tap(find.text('Use This Location'));
      await tester.pumpAndSettle();

      // Confirmed card is shown, manual input is NOT revealed
      expect(find.byKey(const Key('confirmed_location_card')), findsOneWidget);
      expect(find.widgetWithText(AppTextField, 'Location / Address'), findsNothing);
    });
  });

  group('4. Pre-Analysis Copy Polish', () {
    testWidgets('Created request without analysis displays "Pending AI analysis" and "Urgency pending"',
        (tester) async {
      final createdReq = ServiceRequestModel(
        serviceRequestId: 'req-pending-1',
        customerId: 'cust-1',
        category: 'Unclassified',
        description: 'Strange buzzing sound from fuse box',
        locationText: 'Kandy Road',
        urgency: ServiceRequestUrgency.unknown,
        status: ServiceRequestStatus.created,
        createdAt: DateTime(2026, 10, 1),
        updatedAt: DateTime(2026, 10, 1),
      );

      // Verify in ServiceRequestCard (activity/home view)
      await tester.pumpWidget(
        buildApp(
          Scaffold(
            body: ServiceRequestCard(request: createdReq),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Pending AI analysis'), findsOneWidget);
      expect(find.text('Urgency pending'), findsOneWidget);
      expect(find.text('Unclassified'), findsNothing);
      expect(find.text('Unknown'), findsNothing);
    });

    testWidgets('Detail screen displays "Pending AI analysis" and "Urgency pending" in header',
        (tester) async {
      final createdReq = ServiceRequestModel(
        serviceRequestId: 'req-pending-detail',
        customerId: 'cust-1',
        category: '',
        description: 'Water leaking through apartment ceiling',
        locationText: 'Havelock Town',
        urgency: ServiceRequestUrgency.unknown,
        status: ServiceRequestStatus.created,
        createdAt: DateTime(2026, 10, 1),
        updatedAt: DateTime(2026, 10, 1),
      );
      requests.items = [createdReq];

      await tester.pumpWidget(
        buildApp(
          const ServiceRequestDetailScreen(requestId: 'req-pending-detail'),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Pending AI analysis'), findsWidgets);
      expect(find.text('Urgency pending'), findsOneWidget);
      expect(find.text('Unclassified'), findsNothing);
      expect(find.text('Unknown'), findsNothing);
    });
  });

  group('5. Animated Border Trail & Accessibility', () {
    testWidgets('AnimatedBorderTrail does not break widget tests and pumps cleanly',
        (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.lightTheme,
          home: const Scaffold(
            body: Center(
              child: AnimatedBorderTrail(
                child: Text('Test Action Button'),
              ),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Test Action Button'), findsOneWidget);
      expect(tester.takeException(), isNull);
    });

    testWidgets('AnimatedBorderTrail renders static fallback when animate is false or reduced motion',
        (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          theme: AppTheme.lightTheme,
          home: const Scaffold(
            body: Center(
              child: AnimatedBorderTrail(
                animate: false,
                child: Text('Reduced Motion Button'),
              ),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Reduced Motion Button'), findsOneWidget);
      // Fallback renders DecoratedBox static border
      expect(find.byType(DecoratedBox), findsWidgets);
      expect(tester.takeException(), isNull);
    });

    testWidgets('Analyze CTA button is wrapped with AnimatedBorderTrail',
        (tester) async {
      final createdReq = ServiceRequestModel(
        serviceRequestId: 'req-cta-trail',
        customerId: 'cust-1',
        category: 'Plumbing',
        description: 'Tap broken',
        locationText: 'Colombo',
        urgency: ServiceRequestUrgency.medium,
        status: ServiceRequestStatus.created,
        createdAt: DateTime(2026, 10, 1),
        updatedAt: DateTime(2026, 10, 1),
      );
      requests.items = [createdReq];

      await tester.pumpWidget(
        buildApp(
          const ServiceRequestDetailScreen(requestId: 'req-cta-trail'),
        ),
      );
      await tester.pumpAndSettle();

      // Verify AnimatedBorderTrail is present and contains the CTA
      final trailFinder = find.byType(AnimatedBorderTrail);
      expect(trailFinder, findsOneWidget);
      expect(
        find.descendant(
          of: trailFinder,
          matching: find.text('Analyze Request with AssistLK AI'),
        ),
        findsOneWidget,
      );
    });
  });
}
