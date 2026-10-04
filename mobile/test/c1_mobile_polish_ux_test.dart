import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
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
import 'package:mobile/features/service_requests/models/canonical_service_category.dart';
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
      expect(find.text('Create Service Request'), findsOneWidget);
      final appBar = tester.widget<AppBar>(find.byType(AppBar));
      expect(appBar.backgroundColor, AppColors.primary);
      expect(appBar.foregroundColor, Colors.white);
      expect(appBar.elevation, 0);
      expect(appBar.systemOverlayStyle, SystemUiOverlayStyle.light);

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

    testWidgets(
        'Create Service Request flow maintains consistent navy primary AppBar across all 3 steps',
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

      // Step 0: Details
      expect(find.text('Create Service Request'), findsOneWidget);
      final appBarDetails = tester.widget<AppBar>(find.byType(AppBar));
      expect(appBarDetails.backgroundColor, AppColors.primary);
      expect(appBarDetails.foregroundColor, Colors.white);
      expect(appBarDetails.elevation, 0);
      expect(appBarDetails.systemOverlayStyle, SystemUiOverlayStyle.light);

      // Advance to Step 1: Location
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Problem Description'),
        'Ceiling pipe leaking severely into hallway',
      );
      await tester.tap(find.text('Continue to Location'));
      await tester.pumpAndSettle();

      // Step 1: Location
      expect(find.text('Create Service Request'), findsOneWidget);
      final appBarLocation = tester.widget<AppBar>(find.byType(AppBar));
      expect(appBarLocation.backgroundColor, AppColors.primary);
      expect(appBarLocation.foregroundColor, Colors.white);
      expect(appBarLocation.elevation, 0);
      expect(appBarLocation.systemOverlayStyle, SystemUiOverlayStyle.light);

      // Advance to Step 2: Review
      geocoding.forwardReply = (addr) async => [
        ForwardGeocodeCandidate(
          displayAddress: '123 Galle Road, Colombo 03',
          latitude: 6.91,
          longitude: 79.85,
          placeId: 'cand-123',
          source: 'OpenStreetMap',
        ),
      ];
      await tester.tap(find.text('Enter Manually'));
      await tester.pumpAndSettle();
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Location / Address'),
        '123 Galle Road, Colombo 03',
      );
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('resolve_address_button')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('use_forward_location_button')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Review Request'));
      await tester.pumpAndSettle();

      // Step 2: Review
      expect(find.text('Create Service Request'), findsOneWidget);
      final appBarReview = tester.widget<AppBar>(find.byType(AppBar));
      expect(appBarReview.backgroundColor, AppColors.primary);
      expect(appBarReview.foregroundColor, Colors.white);
      expect(appBarReview.elevation, 0);
      expect(appBarReview.systemOverlayStyle, SystemUiOverlayStyle.light);
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

  group('6. Request Flow Navy App Bar & Dynamic Description Placeholder Consistency', () {
    // 1. Details app bar is navy
    testWidgets('Details app bar uses AssistLK navy background and white foreground',
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

      final appBarFinder = find.byType(AppBar);
      expect(appBarFinder, findsOneWidget);
      final appBar = tester.widget<AppBar>(appBarFinder);
      expect(appBar.backgroundColor, AppColors.primary);
      expect(appBar.foregroundColor, Colors.white);
      expect(appBar.elevation, 0);
      expect(appBar.systemOverlayStyle, SystemUiOverlayStyle.light);
      expect(find.text('Create Service Request'), findsOneWidget);
    });

    // 2. Request Details app bar is navy in every request-flow state
    for (final status in [
      ServiceRequestStatus.created,
      ServiceRequestStatus.analyzing,
      ServiceRequestStatus.awaitingInformation,
      ServiceRequestStatus.analyzed,
      ServiceRequestStatus.readyForMatching,
      ServiceRequestStatus.cancelled,
    ]) {
      testWidgets('Request Details app bar is navy in status: ${status.name}',
          (tester) async {
        final req = ServiceRequestModel(
          serviceRequestId: 'req-${status.name}',
          customerId: 'cust-1',
          category: 'Plumbing',
          description: 'Water leak',
          locationText: 'Colombo',
          urgency: ServiceRequestUrgency.medium,
          status: status,
          createdAt: DateTime(2026, 10, 1),
          updatedAt: DateTime(2026, 10, 1),
        );
        requests.items = [req];

        await tester.pumpWidget(
          buildApp(
            ServiceRequestDetailScreen(requestId: 'req-${status.name}'),
          ),
        );
        if (status == ServiceRequestStatus.analyzing) {
          await tester.pump();
          await tester.pump(const Duration(milliseconds: 100));
          await tester.pump();
        } else {
          await tester.pumpAndSettle();
        }

        final appBarFinder = find.byType(AppBar);
        expect(appBarFinder, findsOneWidget);
        final appBar = tester.widget<AppBar>(appBarFinder);
        expect(appBar.backgroundColor, AppColors.primary);
        expect(appBar.foregroundColor, Colors.white);
        expect(appBar.elevation, 0);
        expect(appBar.systemOverlayStyle, SystemUiOverlayStyle.light);
        expect(find.text('Request Details'), findsOneWidget);
      });
    }

    testWidgets('Request Details app bar is navy in loading and error states',
        (tester) async {
      requests.items = [];
      await tester.pumpWidget(
        buildApp(
          const ServiceRequestDetailScreen(requestId: 'non-existent-id'),
        ),
      );
      await tester.pumpAndSettle();

      final appBarFinder = find.byType(AppBar);
      expect(appBarFinder, findsOneWidget);
      final appBar = tester.widget<AppBar>(appBarFinder);
      expect(appBar.backgroundColor, AppColors.primary);
      expect(appBar.foregroundColor, Colors.white);
      expect(find.text('Request Details'), findsOneWidget);
    });

    // 3. Title, back button, and actions remain readable on navy
    testWidgets('app bar title, back icon, and cancel action remain readable on navy',
        (tester) async {
      final req = ServiceRequestModel(
        serviceRequestId: 'req-cancel-check',
        customerId: 'cust-1',
        category: 'Plumbing',
        description: 'Water pipe leaking',
        locationText: 'Colombo',
        urgency: ServiceRequestUrgency.low,
        status: ServiceRequestStatus.created,
        createdAt: DateTime(2026, 10, 1),
        updatedAt: DateTime(2026, 10, 1),
      );
      requests.items = [req];

      await tester.pumpWidget(
        buildApp(
          Navigator(
            onGenerateRoute: (_) => MaterialPageRoute(
              builder: (_) => const ServiceRequestDetailScreen(
                requestId: 'req-cancel-check',
              ),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      final appBar = tester.widget<AppBar>(find.byType(AppBar));
      expect(appBar.foregroundColor, Colors.white);
      expect(appBar.iconTheme?.color, Colors.white);

      // Cancel button action retains destructive color with high contrast on navy
      final cancelIconFinder = find.byIcon(Icons.cancel_outlined);
      expect(cancelIconFinder, findsOneWidget);
      final iconWidget = tester.widget<Icon>(cancelIconFinder);
      expect(iconWidget.color, AppColors.destructiveOnNavy);
    });

    // 4. Plumbing produces plumbing placeholder
    test('Plumbing produces plumbing placeholder', () {
      expect(
        problemDescriptionHintForCategory('Plumbing'),
        'e.g., Water is leaking heavily from the kitchen sink.',
      );
    });

    // 5. Vehicle Repair produces vehicle placeholder
    test('Vehicle Repair produces vehicle placeholder', () {
      expect(
        problemDescriptionHintForCategory('Vehicle Repair'),
        'e.g., My car will not start and makes a clicking sound.',
      );
      expect(
        problemDescriptionHintForCategory('Vehicle Assistance'),
        'e.g., My car will not start and makes a clicking sound.',
      );
    });

    // 6. Electrical produces electrical placeholder
    test('Electrical produces electrical placeholder', () {
      expect(
        problemDescriptionHintForCategory('Electrical'),
        'e.g., The bedroom power outlets suddenly stopped working.',
      );
      expect(
        problemDescriptionHintForCategory('Electrical Services'),
        'e.g., The bedroom power outlets suddenly stopped working.',
      );
    });

    // 7. Appliance Repair produces appliance placeholder
    test('Appliance Repair produces appliance placeholder', () {
      expect(
        problemDescriptionHintForCategory('Appliance Repair'),
        'e.g., The washing machine is not draining water.',
      );
    });

    // 8. Generic AI-identify mode produces neutral placeholder
    test('generic AI-identify mode and unselected category produce neutral placeholder', () {
      expect(
        problemDescriptionHintForCategory(null),
        'e.g., Describe what happened, what is affected, and any symptoms you noticed.',
      );
      expect(
        problemDescriptionHintForCategory(''),
        'e.g., Describe what happened, what is affected, and any symptoms you noticed.',
      );
      expect(
        problemDescriptionHintForCategory('Let AssistLK AI identify'),
        'e.g., Describe what happened, what is affected, and any symptoms you noticed.',
      );
      expect(
        problemDescriptionHintForCategory('Let AI identify'),
        'e.g., Describe what happened, what is affected, and any symptoms you noticed.',
      );
      expect(
        problemDescriptionHintForCategory('Unknown Category'),
        'e.g., Describe what happened, what is affected, and any symptoms you noticed.',
      );
    });

    // Dynamic placeholder updates on category selection
    testWidgets('CreateServiceRequestScreen dynamically updates placeholder when preference changes',
        (tester) async {
      await tester.pumpWidget(
        buildApp(
          CreateServiceRequestScreen(
            locationService: gps,
            geocodingService: geocoding,
            initialCategoryPreference: 'Plumbing',
          ),
        ),
      );
      await tester.pumpAndSettle();

      // Initially Plumbing placeholder
      final fieldFinder = find.widgetWithText(AppTextField, 'Problem Description');
      expect(fieldFinder, findsOneWidget);
      AppTextField field = tester.widget<AppTextField>(fieldFinder);
      expect(field.hint, 'e.g., Water is leaking heavily from the kitchen sink.');

      // Tap Change preference
      await tester.tap(find.text('Change'));
      await tester.pumpAndSettle();

      // Select Electrical
      await tester.tap(find.widgetWithText(ListTile, 'Electrical'));
      await tester.pumpAndSettle();

      field = tester.widget<AppTextField>(fieldFinder);
      expect(field.hint, 'e.g., The bedroom power outlets suddenly stopped working.');

      // Tap Change preference again -> Vehicle Assistance
      await tester.tap(find.text('Change'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(ListTile, 'Vehicle Assistance'));
      await tester.pumpAndSettle();

      field = tester.widget<AppTextField>(fieldFinder);
      expect(field.hint, 'e.g., My car will not start and makes a clicking sound.');

      // Tap Change preference again -> Appliance Repair
      await tester.tap(find.text('Change'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(ListTile, 'Appliance Repair'));
      await tester.pumpAndSettle();

      field = tester.widget<AppTextField>(fieldFinder);
      expect(field.hint, 'e.g., The washing machine is not draining water.');

      // Tap Change preference again -> Let AssistLK AI identify
      await tester.tap(find.text('Change'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Let AssistLK AI identify'));
      await tester.pumpAndSettle();

      field = tester.widget<AppTextField>(fieldFinder);
      expect(
        field.hint,
        'e.g., Describe what happened, what is affected, and any symptoms you noticed.',
      );
    });

    // 9. Changing category does not clear typed description
    testWidgets('changing category does not clear typed description',
        (tester) async {
      await tester.pumpWidget(
        buildApp(
          CreateServiceRequestScreen(
            locationService: gps,
            geocodingService: geocoding,
            initialCategoryPreference: 'Plumbing',
          ),
        ),
      );
      await tester.pumpAndSettle();

      // Type custom description
      const typedText = 'pipe burst near bathroom';
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Problem Description'),
        typedText,
      );
      await tester.pumpAndSettle();

      // Verify entered text
      expect(find.text(typedText), findsOneWidget);

      // Change category to Electrical
      await tester.tap(find.text('Change'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(ListTile, 'Electrical'));
      await tester.pumpAndSettle();

      // Typed text remains completely intact
      expect(find.text(typedText), findsOneWidget);
      final textFormField = tester.widget<TextFormField>(
        find.widgetWithText(TextFormField, 'Problem Description'),
      );
      expect(textFormField.controller?.text, typedText);

      // Change category to AI identify
      await tester.tap(find.text('Change'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Let AssistLK AI identify'));
      await tester.pumpAndSettle();

      // Typed text still remains completely intact
      expect(find.text(typedText), findsOneWidget);
      expect(textFormField.controller?.text, typedText);
    });

    // 10. Placeholder text is not submitted as request description
    testWidgets('placeholder text is not submitted as request description',
        (tester) async {
      await tester.pumpWidget(
        buildApp(
          CreateServiceRequestScreen(
            locationService: gps,
            geocodingService: geocoding,
            initialCategoryPreference: 'Plumbing',
          ),
        ),
      );
      await tester.pumpAndSettle();

      // Controller is empty
      final textFormField = tester.widget<TextFormField>(
        find.widgetWithText(TextFormField, 'Problem Description'),
      );
      expect(textFormField.controller?.text, isEmpty);

      // Attempt to proceed with only the placeholder visible
      await tester.tap(find.text('Continue to Location'));
      await tester.pumpAndSettle();

      // Validation triggers error and blocks navigation - placeholder is not accepted as description
      expect(
        find.text('Please describe the problem you are experiencing.'),
        findsOneWidget,
      );
      expect(find.text('Location / Address'), findsNothing);
    });
  });
}
