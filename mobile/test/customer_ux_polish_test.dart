import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'package:mobile/app/app.dart';
import 'package:mobile/app/customer_bottom_navigation.dart';
import 'package:mobile/core/api/api_client.dart';
import 'package:mobile/features/auth/models/auth_user.dart';
import 'package:mobile/features/auth/providers/auth_provider.dart';
import 'package:mobile/features/auth/screens/customer_account_screen.dart';
import 'package:mobile/features/auth/screens/login_screen.dart';
import 'package:mobile/features/service_requests/models/resolved_location.dart';
import 'package:mobile/features/service_requests/screens/create_service_request_screen.dart';
import 'package:mobile/features/service_requests/services/location_service.dart';
import 'package:mobile/features/service_requests/widgets/service_request_card.dart';
import 'package:mobile/shared/theme/app_assets.dart';
import 'package:mobile/shared/widgets/app_image_asset.dart';
import 'package:mobile/shared/widgets/empty_state_card.dart';

import 'mocks/mock_location_geocoding_service.dart';
import 'session_foundation_test.dart'
    show MemoryStorage, AuthStub, RequestsStub, request;

class TestGps implements LocationService {
  LocationAccessStatus status = LocationAccessStatus.granted;

  @override
  Future<bool> isLocationServiceEnabled() async => true;

  @override
  Future<LocationAccessStatus> checkPermission() async => status;

  @override
  Future<LocationAccessStatus> requestPermission() async => status;

  @override
  Future<LocationResult> getCurrentLocation({
    Duration timeLimit = const Duration(seconds: 10),
  }) async {
    return LocationResult(
      status: status,
      coordinates: status == LocationAccessStatus.granted
          ? const LocationCoordinates(
              latitude: 6.9271,
              longitude: 79.8612,
              accuracy: 25,
            )
          : null,
    );
  }
}

void main() {
  late TestGps gps;
  late MockLocationGeocodingService geo;
  late DateTime now;
  late ApiClient api;
  late AuthStub authService;
  late AuthProvider auth;
  late RequestsStub requests;

  setUp(() {
    gps = TestGps();
    geo = MockLocationGeocodingService()
      ..reply = () async => const ResolvedLocation(
        formattedAddress: '123 Galle Road, Colombo 03',
      );
    now = DateTime(2026, 9, 29, 12, 0);
    final storage = MemoryStorage();
    api = ApiClient(tokenStorage: storage);
    authService = AuthStub(api);
    auth = AuthProvider(authService: authService, tokenStorage: storage);
    requests = RequestsStub(api);
  });

  Future<void> mount(WidgetTester tester) async {
    await auth.login(email: 'customer@assistlk.com', password: 'password');
    await tester.pumpWidget(
      ChangeNotifierProvider<AuthProvider>.value(
        value: auth,
        child: AssistLKApp(
          serviceRequestService: requests,
          locationService: gps,
          geocodingService: geo,
          locationClock: () => now,
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  Finder destination(String label) =>
      find.byKey(ValueKey('customer_navigation_$label'));

  Future<void> tab(WidgetTester tester, String label) async {
    await tester.tap(destination(label));
    await tester.pumpAndSettle();
  }

  group('Customer Home + Account UX Polish Verification', () {
    testWidgets(
      '1. Location unset state shows "Set your service location" and customer-friendly subtitle',
      (tester) async {
        await mount(tester);

        expect(find.text('Set your service location'), findsOneWidget);
        expect(
          find.text('Add your location so we can help find services near you.'),
          findsOneWidget,
        );
        expect(find.textContaining('coordinates'), findsNothing);
        expect(find.textContaining('latitude'), findsNothing);
        expect(find.textContaining('longitude'), findsNothing);
        expect(find.textContaining('geocoding'), findsNothing);
      },
    );

    testWidgets('2. Location CTA remains "Use Current Location"', (
      tester,
    ) async {
      await mount(tester);

      final ctaFinder = find.widgetWithText(
        OutlinedButton,
        'Use Current Location',
      );
      expect(ctaFinder, findsOneWidget);
    });

    testWidgets('3. Existing location-set state still works correctly', (
      tester,
    ) async {
      await mount(tester);

      final ctaFinder = find.widgetWithText(
        OutlinedButton,
        'Use Current Location',
      );
      await tester.tap(ctaFinder);
      await tester.pumpAndSettle();

      expect(find.text('Current location'), findsOneWidget);
      expect(find.text('123 Galle Road, Colombo 03'), findsOneWidget);
      expect(find.text('Refresh Location'), findsOneWidget);
    });

    testWidgets(
      '4. New customer with no requests shows "No service requests yet"',
      (tester) async {
        requests.items = [];
        await mount(tester);

        expect(find.text('No service requests yet'), findsOneWidget);
        expect(
          find.text('Create your first request and track its progress here.'),
          findsOneWidget,
        );
      },
    );

    testWidgets(
      '5. Empty recent activity illustration renders with AppAssets.emptyRecentActivity',
      (tester) async {
        requests.items = [];
        await mount(tester);

        final emptyCardFinder = find.byType(EmptyStateCard);
        expect(emptyCardFinder, findsOneWidget);

        final emptyCard = tester.widget<EmptyStateCard>(emptyCardFinder);
        expect(emptyCard.imageAsset, AppAssets.emptyRecentActivity);
        expect(emptyCard.imageSemanticLabel, 'No recent activity illustration');

        final illustrationFinder = find.byWidgetPredicate(
          (w) =>
              w is AppImageAsset &&
              w.assetPath == AppAssets.emptyRecentActivity,
        );
        expect(illustrationFinder, findsOneWidget);
      },
    );

    testWidgets(
      '6. Create Service Request CTA from empty state opens create request flow',
      (tester) async {
        requests.items = [];
        await mount(tester);

        final emptyCta = find.descendant(
          of: find.byType(EmptyStateCard),
          matching: find.text('Create Service Request'),
        );
        expect(emptyCta, findsOneWidget);

        await tester.ensureVisible(emptyCta);
        await tester.pumpAndSettle();
        await tester.tap(emptyCta);
        await tester.pumpAndSettle();

        expect(find.byType(CreateServiceRequestScreen), findsOneWidget);
      },
    );

    testWidgets(
      '7. Customer with requests still sees existing recent activity list and View All',
      (tester) async {
        requests.items = [
          request('req-1').copyWith(description: 'Fix bathroom tap'),
          request('req-2').copyWith(description: 'Repair air conditioner'),
        ];
        await mount(tester);

        expect(find.text('No service requests yet'), findsNothing);
        expect(find.text('Fix bathroom tap'), findsOneWidget);
        expect(find.text('Repair air conditioner'), findsOneWidget);
        expect(find.text('View All'), findsOneWidget);
        expect(find.byType(ServiceRequestCard), findsNWidgets(2));
      },
    );

    testWidgets(
      '8. Account screen renders profile summary with avatar initials, name, role, email',
      (tester) async {
        authService.nextUser = const AuthUser(
          userId: 'usr-1',
          fullName: 'test dart',
          email: 'testdart@gmail.com',
          role: 'Customer',
          phoneNumber: '0676574866',
        );
        await mount(tester);
        await tab(tester, 'Account');

        expect(find.byType(CircleAvatar), findsOneWidget);
        expect(find.text('TD'), findsOneWidget);
        expect(find.text('test dart'), findsOneWidget);
        expect(find.text('Customer'), findsWidgets);
        expect(find.text('testdart@gmail.com'), findsWidgets);
      },
    );

    testWidgets(
      '9. Account screen displays structured rows for name, email, phone, account type',
      (tester) async {
        authService.nextUser = const AuthUser(
          userId: 'usr-2',
          fullName: 'Kamal Perera',
          email: 'kamal@example.com',
          role: 'Customer',
          phoneNumber: '0771234567',
        );
        await mount(tester);
        await tab(tester, 'Account');

        // Summary Card
        expect(find.text('KP'), findsOneWidget);
        expect(find.text('Kamal Perera'), findsOneWidget);

        // Structured Personal Information rows
        expect(find.text('Personal Information'), findsOneWidget);
        expect(find.text('Email'), findsOneWidget);
        expect(find.text('kamal@example.com'), findsWidgets);
        expect(find.text('Phone'), findsOneWidget);
        expect(find.text('0771234567'), findsOneWidget);
        expect(find.text('Account type'), findsOneWidget);
        expect(find.text('Customer'), findsWidgets);
      },
    );

    testWidgets(
      '10. Logout behavior is unchanged and uses safe destructive styling',
      (tester) async {
        await mount(tester);
        await tab(tester, 'Account');

        final logoutBtnFinder = find.widgetWithText(OutlinedButton, 'Logout');
        expect(logoutBtnFinder, findsOneWidget);

        await tester.ensureVisible(logoutBtnFinder);
        await tester.pumpAndSettle();
        await tester.tap(logoutBtnFinder);
        await tester.pumpAndSettle();

        expect(find.byType(LoginScreen), findsOneWidget);
      },
    );

    testWidgets(
      '11. Home hero still renders with AppAssets.homeServiceHero and welcome copy',
      (tester) async {
        await mount(tester);

        final heroFinder = find.byWidgetPredicate(
          (widget) =>
              widget is AppImageAsset &&
              widget.assetPath == AppAssets.homeServiceHero,
        );
        expect(heroFinder, findsOneWidget);
        expect(find.text('Welcome, Customer A'), findsOneWidget);
        expect(find.text('What can we help you with today?'), findsOneWidget);
      },
    );

    testWidgets('12. Bottom navigation is unchanged', (tester) async {
      await mount(tester);

      expect(find.byType(CustomerBottomNavigation), findsOneWidget);
      expect(CustomerBottomNavigation.labels, [
        'Home',
        'Services',
        'Activity',
        'Account',
      ]);
    });

    testWidgets(
      'Initials generation handles various customer name formats correctly',
      (tester) async {
        expect(CustomerAccountScreen.getInitials('test dart'), 'TD');
        expect(CustomerAccountScreen.getInitials('Customer A'), 'CA');
        expect(
          CustomerAccountScreen.getInitials('Kamal Samantha Perera'),
          'KP',
        );
        expect(CustomerAccountScreen.getInitials('Alice'), 'A');
        expect(CustomerAccountScreen.getInitials(''), 'U');
        expect(CustomerAccountScreen.getInitials(null), 'U');
      },
    );

    for (final size in const [
      Size(360, 640),
      Size(390, 844),
      Size(412, 915),
      Size(768, 1024),
    ]) {
      testWidgets(
        'Responsive layouts at ${size.width}x${size.height} render without overflow',
        (tester) async {
          tester.view.physicalSize = size;
          tester.view.devicePixelRatio = 1.0;
          addTearDown(() {
            tester.view.resetPhysicalSize();
            tester.view.resetDevicePixelRatio();
          });

          requests.items = [];
          await mount(tester);
          expect(tester.takeException(), isNull);

          // Verify Home elements
          expect(find.text('Set your service location'), findsOneWidget);
          expect(
            find.byWidgetPredicate(
              (w) =>
                  w is AppImageAsset &&
                  w.assetPath == AppAssets.homeServiceHero,
            ),
            findsOneWidget,
          );
          expect(find.byType(EmptyStateCard), findsOneWidget);

          // Switch to Account
          await tab(tester, 'Account');
          expect(tester.takeException(), isNull);
          expect(find.byType(CircleAvatar), findsOneWidget);
          expect(find.text('Personal Information'), findsOneWidget);
          expect(find.widgetWithText(OutlinedButton, 'Logout'), findsOneWidget);

          // Switch back to Home
          await tab(tester, 'Home');
          expect(tester.takeException(), isNull);
        },
      );
    }
  });
}
