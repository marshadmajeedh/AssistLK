import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'package:mobile/app/app.dart';
import 'package:mobile/app/customer_bottom_navigation.dart';
import 'package:mobile/core/api/api_client.dart';
import 'package:mobile/features/auth/providers/auth_provider.dart';
import 'package:mobile/features/customer/widgets/home_location_banner.dart';
import 'package:mobile/features/service_requests/models/resolved_location.dart';
import 'package:mobile/features/service_requests/services/location_service.dart';
import 'package:mobile/features/service_requests/widgets/service_category_shortcuts.dart';
import 'package:mobile/shared/theme/app_assets.dart';
import 'package:mobile/shared/widgets/app_button.dart';
import 'package:mobile/shared/widgets/app_image_asset.dart';

import 'mocks/mock_location_geocoding_service.dart';
import 'session_foundation_test.dart' show MemoryStorage, AuthStub, RequestsStub;

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
          ? const LocationCoordinates(latitude: 6.9, longitude: 79.9, accuracy: 40)
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
      ..reply = () async => const ResolvedLocation(formattedAddress: 'Colombo 03');
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

  group('CustomerHomeScreen Hero Image Restoration', () {
    testWidgets('1. CustomerHomeScreen renders AppAssets.homeServiceHero with correct semantics and fallback', (tester) async {
      await mount(tester);

      final heroFinder = find.byWidgetPredicate(
        (widget) => widget is AppImageAsset && widget.assetPath == AppAssets.homeServiceHero,
      );
      expect(heroFinder, findsOneWidget);

      final heroWidget = tester.widget<AppImageAsset>(heroFinder);
      expect(heroWidget.semanticLabel, 'Home Service Assistance');
      expect(heroWidget.fallbackIcon, Icons.home_repair_service_rounded);
      expect(heroWidget.fit, BoxFit.cover);
    });

    testWidgets('2. Welcome text appears exactly once in the restored hero banner', (tester) async {
      await mount(tester);

      expect(find.text('Welcome, Customer A'), findsOneWidget);
      expect(find.textContaining('Welcome'), findsOneWidget);
    });

    testWidgets('3. HomeLocationBanner still renders above hero banner', (tester) async {
      await mount(tester);

      expect(find.byType(HomeLocationBanner), findsOneWidget);

      final locationCenter = tester.getCenter(find.byType(HomeLocationBanner));
      final heroCenter = tester.getCenter(
        find.byWidgetPredicate(
          (w) => w is AppImageAsset && w.assetPath == AppAssets.homeServiceHero,
        ),
      );
      // HomeLocationBanner is positioned vertically above the hero banner
      expect(locationCenter.dy, lessThan(heroCenter.dy));
    });

    testWidgets('4. Create Service Request CTA still renders below hero banner', (tester) async {
      await mount(tester);

      final ctaFinder = find.widgetWithText(AppButton, 'Create Service Request');
      expect(ctaFinder, findsOneWidget);

      final heroCenter = tester.getCenter(
        find.byWidgetPredicate(
          (w) => w is AppImageAsset && w.assetPath == AppAssets.homeServiceHero,
        ),
      );
      final ctaCenter = tester.getCenter(ctaFinder);
      expect(heroCenter.dy, lessThan(ctaCenter.dy));
    });

    testWidgets('5. Explore services section still renders', (tester) async {
      await mount(tester);

      expect(find.text('Explore services'), findsOneWidget);
    });

    testWidgets('6. Category shortcuts/cards still render', (tester) async {
      await mount(tester);

      final shortcutsFinder = find.byType(ServiceCategoryShortcuts);
      expect(shortcutsFinder, findsOneWidget);
      expect(find.descendant(of: shortcutsFinder, matching: find.text('Plumbing')), findsOneWidget);
      expect(find.descendant(of: shortcutsFinder, matching: find.text('Electrical')), findsOneWidget);
      expect(find.descendant(of: shortcutsFinder, matching: find.text('Vehicle Assistance')), findsOneWidget);
      expect(find.descendant(of: shortcutsFinder, matching: find.text('Appliance Repair')), findsOneWidget);
    });

    testWidgets('7. No overflow across 360px, 390px, 412px, and tablet widths', (tester) async {
      const viewports = [
        Size(360, 640),
        Size(390, 844),
        Size(412, 915),
        Size(768, 1024),
      ];

      for (final size in viewports) {
        tester.view.physicalSize = size;
        tester.view.devicePixelRatio = 1.0;
        addTearDown(() {
          tester.view.resetPhysicalSize();
          tester.view.resetDevicePixelRatio();
        });

        await mount(tester);
        expect(tester.takeException(), isNull);

        // Ensure key elements are on screen without clipping or exceptions
        expect(find.byType(HomeLocationBanner), findsOneWidget);
        expect(
          find.byWidgetPredicate(
            (w) => w is AppImageAsset && w.assetPath == AppAssets.homeServiceHero,
          ),
          findsOneWidget,
        );
        expect(find.text('Welcome, Customer A'), findsOneWidget);
        expect(find.text('What can we help you with today?'), findsOneWidget);

        tester.view.resetPhysicalSize();
        tester.view.resetDevicePixelRatio();
      }
    });

    testWidgets('8. No duplicate welcome copy or subtitle copy', (tester) async {
      await mount(tester);

      expect(find.textContaining('Welcome'), findsOneWidget);
      expect(find.text('What can we help you with today?'), findsOneWidget);
      expect(find.text('customer@assistlk.com'), findsNothing);
    });

    testWidgets('9. Bottom navigation remains unaffected in CustomerAppShell', (tester) async {
      await mount(tester);

      expect(find.byType(CustomerBottomNavigation), findsOneWidget);
      expect(
        tester.widget<CustomerBottomNavigation>(find.byType(CustomerBottomNavigation)).selectedIndex,
        0,
      );
      expect(
        CustomerBottomNavigation.labels,
        ['Home', 'Services', 'Activity', 'Account'],
      );
    });
  });
}
