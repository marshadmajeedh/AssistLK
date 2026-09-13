import 'package:mobile/app/customer_bottom_navigation.dart';
import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:dio/dio.dart';
import 'package:mobile/app/app.dart';
import 'package:mobile/app/customer_app_shell.dart';
import 'package:mobile/core/api/api_client.dart';
import 'package:mobile/features/auth/providers/auth_provider.dart';
import 'package:mobile/features/providers/provider_home_screen.dart';
import 'package:mobile/features/customer/models/location_suggestion.dart';
import 'package:mobile/features/customer/providers/customer_location_provider.dart';
import 'package:mobile/features/service_requests/models/location_source.dart';
import 'package:mobile/features/service_requests/models/resolved_location.dart';
import 'package:mobile/features/service_requests/providers/location_selection_controller.dart';
import 'package:mobile/features/service_requests/screens/create_service_request_screen.dart';
import 'package:mobile/features/service_requests/screens/customer_home_screen.dart';
import 'package:mobile/features/service_requests/screens/edit_service_request_screen.dart';
import 'package:mobile/features/service_requests/services/location_service.dart';
import 'package:mobile/features/service_requests/widgets/location_attribution.dart';
import 'package:mobile/features/service_requests/widgets/location_selection.dart';
import 'package:mobile/features/service_requests/widgets/service_request_card.dart';
import 'package:mobile/shared/theme/app_spacing.dart';
import 'package:mobile/shared/theme/app_theme.dart';
import 'package:mobile/shared/widgets/app_button.dart';
import 'package:mobile/features/service_requests/widgets/service_category_card.dart';
import 'package:mobile/features/service_requests/widgets/service_category_shortcuts.dart';

import 'session_foundation_test.dart'
    show AuthStub, MemoryStorage, ReplyAdapter, request;
import 'customer_app_shell_test.dart' show ShellRequests;
import 'mocks/mock_location_geocoding_service.dart';

const addressA =
    'Ramya Mawatha, Battaramulla North, Battaramulla, Western Province, Sri Lanka';
const addressB = 'Galle Road, Colombo, Sri Lanka';

class HomeGps implements LocationService {
  int calls = 0;
  int permissionCalls = 0;
  LocationAccessStatus status = LocationAccessStatus.granted;
  Completer<LocationResult>? pending;
  @override
  Future<bool> isLocationServiceEnabled() async {
    permissionCalls++;
    return true;
  }

  @override
  Future<LocationAccessStatus> checkPermission() async {
    permissionCalls++;
    return status;
  }

  @override
  Future<LocationAccessStatus> requestPermission() async {
    permissionCalls++;
    return status;
  }

  @override
  Future<LocationResult> getCurrentLocation({
    Duration timeLimit = const Duration(seconds: 10),
  }) async {
    calls++;
    return pending != null
        ? pending!.future
        : LocationResult(
            status: status,
            coordinates: status == LocationAccessStatus.granted
                ? const LocationCoordinates(
                    latitude: 6.9,
                    longitude: 79.9,
                    accuracy: 40,
                  )
                : null,
          );
  }
}

void main() {
  late HomeGps gps;
  late MockLocationGeocodingService geo;
  late DateTime now;
  late ApiClient api;
  late AuthStub authService;
  late AuthProvider auth;
  late ShellRequests requests;
  setUp(() {
    gps = HomeGps();
    geo = MockLocationGeocodingService()
      ..reply = () async => const ResolvedLocation(formattedAddress: addressA);
    now = DateTime.now();
    final storage = MemoryStorage();
    api = ApiClient(tokenStorage: storage);
    authService = AuthStub(api);
    auth = AuthProvider(authService: authService, tokenStorage: storage);
    requests = ShellRequests(api);
  });
  CustomerLocationProvider provider() =>
      CustomerLocationProvider(gps: gps, geocoding: geo, clock: () => now);
  LocationSuggestion snapshot({
    LocationSource source = LocationSource.openStreetMap,
    DateTime? capturedAt,
  }) => LocationSuggestion(
    location: const ResolvedLocation(formattedAddress: addressA),
    latitude: 6.9,
    longitude: 79.9,
    accuracyMeters: 40,
    source: source,
    capturedAt: capturedAt ?? now,
  );
  Future<void> mount(WidgetTester tester) async {
    await auth.login(email: 'a@example.com', password: 'password');
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

  CustomerLocationProvider homeProvider(WidgetTester tester) => tester
      .element(find.byType(CustomerAppShell, skipOffstage: false))
      .read<CustomerLocationProvider>();
  Future<void> tap(WidgetTester tester, String text) async {
    final target = find.text(text).first;
    await tester.ensureVisible(target);
    await tester.tap(target);
    await tester.pumpAndSettle();
  }

  Future<void> capture(WidgetTester tester) async {
    await tap(tester, 'Use Current Location');
  }

  Future<void> openLocation(WidgetTester tester) async {
    await tap(tester, 'Create Service Request');
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Problem Description'),
      'The kitchen pipe has a persistent leak',
    );
    await tap(tester, 'Next: Location');
  }

  LocationSelectionController draft(WidgetTester tester) => tester
      .widget<LocationSelection>(find.byType(LocationSelection))
      .controller;
  Future<void> unmount(WidgetTester tester) =>
      tester.pumpWidget(const SizedBox.shrink());

  test(
    'freshness has a deterministic 15 minute boundary and rejects future dates',
    () {
      final suggestion = snapshot();
      expect(suggestion.isFresh(now), isTrue);
      expect(
        suggestion.isFresh(now.add(const Duration(minutes: 14, seconds: 59))),
        isTrue,
      );
      expect(suggestion.isFresh(now.add(const Duration(minutes: 15))), isFalse);
      expect(
        suggestion.isFresh(now.subtract(const Duration(seconds: 1))),
        isFalse,
      );
    },
  );

  test(
    'capture progresses through GPS and geocoding and records actual data',
    () async {
      final p = provider();
      addTearDown(p.dispose);
      final states = <CustomerLocationState>[];
      p.addListener(() => states.add(p.state));
      expect(gps.calls, 0);
      expect(gps.permissionCalls, 0);
      await p.capture();
      expect(states, [
        CustomerLocationState.capturing,
        CustomerLocationState.resolving,
        CustomerLocationState.resolved,
      ]);
      expect(p.freshSuggestion!.latitude, 6.9);
      expect(p.freshSuggestion!.longitude, 79.9);
      expect(p.freshSuggestion!.accuracyMeters, 40);
      expect(p.freshSuggestion!.source, LocationSource.openStreetMap);
      expect(geo.latitude, 6.9);
      expect(geo.longitude, 79.9);
      now = now.add(const Duration(minutes: 15));
      expect(p.freshSuggestion, isNull);
      expect(p.suggestion, isNotNull);
    },
  );

  for (final status in [
    LocationAccessStatus.denied,
    LocationAccessStatus.deniedForever,
    LocationAccessStatus.servicesDisabled,
    LocationAccessStatus.timeout,
    LocationAccessStatus.error,
  ]) {
    testWidgets('Home handles $status and retries without automatic checks', (
      tester,
    ) async {
      gps.status = status;
      await mount(tester);
      expect(gps.calls, 0);
      expect(gps.permissionCalls, 0);
      await capture(tester);
      expect(find.text('Try Again'), findsOneWidget);
      expect(geo.calls, 0);
      if (status == LocationAccessStatus.servicesDisabled) {
        expect(
          find.textContaining('Location services are turned off.'),
          findsOneWidget,
        );
      }
      if (status == LocationAccessStatus.deniedForever) {
        expect(find.textContaining('device settings'), findsOneWidget);
      }
      gps.status = LocationAccessStatus.granted;
      await tap(tester, 'Try Again');
      expect(find.text(addressA), findsOneWidget);
      await unmount(tester);
    });
  }

  testWidgets(
    'Home layout, shared recent data and View All do not duplicate fetch',
    (tester) async {
      requests.items = List.generate(
        6,
        (i) => request('$i').copyWith(createdAt: now.add(Duration(minutes: i))),
      );
      await mount(tester);
      expect(find.text('Welcome, Customer A'), findsOneWidget);
      expect(find.text('A@example.com'), findsNothing);
      expect(find.text('Create Service Request'), findsOneWidget);
      expect(find.text('Explore services'), findsOneWidget);
      expect(find.text('Describe Problem'), findsOneWidget);
      expect(find.text('Recent Activity'), findsOneWidget);
      expect(find.byType(ServiceRequestCard), findsNWidgets(3));
      expect(
        tester
            .widgetList<ServiceRequestCard>(find.byType(ServiceRequestCard))
            .map((c) => c.request.serviceRequestId),
        ['5', '4', '3'],
      );
      expect(gps.calls, 0);
      expect(requests.loads, 1);
      await tap(tester, 'View All');
      expect(
        tester.widget<CustomerBottomNavigation>(find.byType(CustomerBottomNavigation)).selectedIndex,
        2,
      );
      expect(find.byType(Navigator), findsOneWidget);
      expect(requests.loads, 1);
    },
  );

  testWidgets(
    'Home capture and resolving states precede address and accuracy',
    (tester) async {
      gps.pending = Completer<LocationResult>();
      final resolved = Completer<ResolvedLocation>();
      geo.reply = () => resolved.future;
      await mount(tester);
      await tester.tap(find.text('Use Current Location'));
      await tester.pump();
      expect(find.text('Finding your location…'), findsOneWidget);
      gps.pending!.complete(
        const LocationResult(
          status: LocationAccessStatus.granted,
          coordinates: LocationCoordinates(
            latitude: 6.9,
            longitude: 79.9,
            accuracy: 40,
          ),
        ),
      );
      await tester.pump();
      expect(find.text('Finding your address…'), findsOneWidget);
      resolved.complete(const ResolvedLocation(formattedAddress: addressA));
      await tester.pumpAndSettle();
      expect(find.text(addressA), findsOneWidget);
      expect(find.text('Accuracy approximately 40 m'), findsOneWidget);
      expect(find.byType(LocationAttribution), findsOneWidget);
      expect(find.textContaining('https://'), findsNothing);
      gps.pending = null;
      geo.reply = () async =>
          const ResolvedLocation(formattedAddress: addressB);
      await tap(tester, 'Refresh Location');
      expect(gps.calls, 2);
      expect(find.text(addressB), findsOneWidget);
      await unmount(tester);
    },
  );

  testWidgets('geocoding failure supports retry', (tester) async {
    geo.reply = () async => throw Exception('unavailable');
    await mount(tester);
    await capture(tester);
    expect(find.textContaining('Could not find an address'), findsOneWidget);
    expect(homeProvider(tester).freshSuggestion, isNull);
    geo.reply = () async => const ResolvedLocation(formattedAddress: addressA);
    await tap(tester, 'Try Again');
    expect(find.text(addressA), findsOneWidget);
    await unmount(tester);
  });

  testWidgets(
    'expiry updates Home label and omits suggestion from new Create',
    (tester) async {
      await mount(tester);
      await capture(tester);
      now = now.add(const Duration(minutes: 15));
      await tester.pump(const Duration(minutes: 15));
      await tester.pumpAndSettle();
      expect(find.text('Last captured location'), findsOneWidget);
      expect(gps.calls, 1);
      await openLocation(tester);
      expect(draft(tester).preview, isNull);
      expect(draft(tester).latitude, isNull);
      await unmount(tester);
    },
  );

  testWidgets('Home pull refresh only refreshes activity', (tester) async {
    await mount(tester);
    final scroll = find.descendant(
      of: find.byType(CustomerHomeScreen),
      matching: find.byType(Scrollable),
    );
    await tester.drag(scroll.first, const Offset(0, 350));
    await tester.pumpAndSettle();
    expect(requests.loads, 2);
    expect(gps.calls, 0);
    expect(gps.permissionCalls, 0);
  });

  for (final width in [320.0, 393.0]) {
    for (final scale in [1.0, 2.0]) {
      testWidgets('category cells and location wizard fit $width at $scale', (
        tester,
      ) async {
        tester.view.physicalSize = Size(width, 852);
        tester.view.devicePixelRatio = 1;
        tester.view.padding = const FakeViewPadding(bottom: 24);
        tester.platformDispatcher.textScaleFactorTestValue = scale;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        addTearDown(tester.view.resetPadding);
        addTearDown(tester.view.resetViewInsets);
        addTearDown(tester.platformDispatcher.clearTextScaleFactorTestValue);
        await mount(tester);
        final grid = find.descendant(
          of: find.byType(ServiceCategoryShortcuts).first,
          matching: find.byType(ServiceCategoryCard),
        );
        final sizes = tester.getSize(grid.first);
        for (final card in grid.evaluate()) {
          expect(tester.getSize(find.byWidget(card.widget)), sizes);
        }
        for (final title in ['Vehicle Assistance', 'Appliance Repair']) {
          final text = find.descendant(of: grid, matching: find.text(title));
          final paragraph = tester.renderObject<RenderParagraph>(text.first);
          expect(paragraph.didExceedMaxLines, isFalse);
        }
        expect(tester.takeException(), isNull);
        await capture(tester);
        await openLocation(tester);
        expect(tester.takeException(), isNull);
        final actions = find.byKey(const Key('wizard_actions'));
        expect(tester.getBottomRight(actions).dy, lessThanOrEqualTo(828));
        tester.view.viewInsets = const FakeViewPadding(bottom: 300);
        await tester.pumpAndSettle();
        expect(tester.getBottomRight(actions).dy, lessThanOrEqualTo(552));
        expect(tester.takeException(), isNull);
        tester.view.viewInsets = const FakeViewPadding();
        await tester.pumpAndSettle();
        await tap(tester, 'Change Location');
        expect(find.text('Address changed with attached GPS'), findsOneWidget);
        expect(find.byKey(const Key('edit_keep_gps_button')), findsOneWidget);
        expect(find.byKey(const Key('edit_remove_gps_button')), findsOneWidget);
        final remove = find.byKey(const Key('edit_remove_gps_button'));
        final field = find.widgetWithText(TextFormField, 'Location / Address');
        expect(tester.getTopLeft(find.text('Remove captured GPS')).dx,
            tester.getTopLeft(field).dx);
        expect(tester.getSize(remove).width, tester.getSize(field).width);
        expect(tester.getSize(remove).height, greaterThanOrEqualTo(48));
        await tester.enterText(
          find.widgetWithText(TextFormField, 'Location / Address'),
          'Manual service address',
        );
        await tap(tester, 'Keep captured GPS for this edited address');
        expect(draft(tester).needsGpsChoice, isFalse);
        await tap(tester, 'Remove captured GPS');
        expect(draft(tester).hasGps, isFalse);
        await tap(tester, 'Next: Review');
        expect(find.text('Review Service Request'), findsOneWidget);
        await tap(tester, 'Back');
        expect(find.text('Service Location'), findsOneWidget);
        expect(tester.takeException(), isNull);
        await unmount(tester);
      });
    }
  }

  testWidgets('suggested location actions have a gap on a narrow screen', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(393, 852);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final c = LocationSelectionController(
      gps: gps,
      geocoding: geo,
      initialSuggestion: snapshot(),
      clock: () => now,
    );
    addTearDown(c.dispose);
    await tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.lightTheme,
        home: Scaffold(
          body: SingleChildScrollView(
            padding: const EdgeInsets.all(AppSpacing.lg),
            child: LocationSelection(controller: c),
          ),
        ),
      ),
    );
    final primary = find.widgetWithText(AppButton, 'Use This Location');
    final secondary = find.widgetWithText(OutlinedButton, 'Change Location');
    expect(
      tester.getTopLeft(secondary).dy - tester.getBottomLeft(primary).dy,
      greaterThanOrEqualTo(AppSpacing.md),
    );
    expect(tester.getSize(primary).width, tester.getSize(secondary).width);
    expect(tester.takeException(), isNull);
    await tap(tester, 'Change Location');
    expect(c.needsGpsChoice, isTrue);
    expect(c.canSubmit, isFalse);
    await unmount(tester);
  });

  testWidgets(
    'fresh snapshot requires Location and explicit confirmation before Review',
    (tester) async {
      await mount(tester);
      await capture(tester);
      await openLocation(tester);
      final c = draft(tester);
      expect(find.text('Suggested service location'), findsOneWidget);
      expect(c.text.text, isEmpty);
      expect(c.canSubmit, isFalse);
      expect(c.latitude, 6.9);
      expect(c.longitude, 79.9);
      expect(c.accuracy, 40);
      await tap(tester, 'Next: Review');
      expect(find.text('Service Location'), findsOneWidget);
      expect(requests.submitted, isNull);
      await tap(tester, 'Use This Location');
      expect(c.source, LocationSource.openStreetMap);
      expect(c.canSubmit, isTrue);
      await tap(tester, 'Next: Review');
      expect(find.text('Review Service Request'), findsOneWidget);
      await tap(tester, 'Submit Request');
      expect(requests.submitted!.latitude, 6.9);
      expect(requests.submitted!.longitude, 79.9);
      expect(requests.submitted!.locationSource, LocationSource.openStreetMap);
      expect(requests.submitted!.toJson().containsKey('accuracy'), isFalse);
      await unmount(tester);
    },
  );

  testWidgets(
    'Home refresh and request edits are isolated; Change Location keeps GPS protection',
    (tester) async {
      await mount(tester);
      await capture(tester);
      final p = homeProvider(tester);
      await openLocation(tester);
      final c = draft(tester);
      geo.reply = () async =>
          const ResolvedLocation(formattedAddress: addressB);
      await p.capture();
      await tester.pumpAndSettle();
      expect(c.preview!.formattedAddress, addressA);
      expect(p.suggestion!.location.formattedAddress, addressB);
      await tap(tester, 'Change Location');
      expect(c.preview, isNull);
      expect(c.needsGpsChoice, isTrue);
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Location / Address'),
        'My manual service address',
      );
      expect(c.validate(), isNotNull);
      c.removeGps();
      await tester.pumpAndSettle();
      expect(c.source, LocationSource.manual);
      expect(c.validate(), isNull);
      expect(p.suggestion!.location.formattedAddress, addressB);
      expect(find.byType(LocationAttribution), findsNothing);
      await unmount(tester);
    },
  );

  for (final category in <String, String?>{
    'Plumbing': 'Plumbing',
    'Electrical': 'Electrical',
    'Vehicle Assistance': 'Vehicle Repair',
    'Appliance Repair': 'Appliance Repair',
    'Describe Problem': null,
  }.entries) {
    testWidgets(
      'Home ${category.key} opens existing create with independent hint and snapshot',
      (tester) async {
        await mount(tester);
        await capture(tester);
        await tap(tester, category.key);
        final create = tester.widget<CreateServiceRequestScreen>(
          find.byType(CreateServiceRequestScreen),
        );
        expect(create.initialCategoryPreference, category.value);
        expect(
          create.initialLocationSuggestion!.location.formattedAddress,
          addressA,
        );
        await unmount(tester);
      },
    );
  }

  testWidgets(
    'Services carries both canonical hint and unconfirmed Home snapshot',
    (tester) async {
      await mount(tester);
      await capture(tester);
      await tester.tap(find.byKey(const ValueKey('customer_navigation_Services')));
      await tester.pumpAndSettle();
      await tap(tester, 'Vehicle Assistance');
      final create = tester.widget<CreateServiceRequestScreen>(
        find.byType(CreateServiceRequestScreen),
      );
      expect(create.initialCategoryPreference, 'Vehicle Repair');
      expect(create.initialLocationSuggestion, isNotNull);
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Problem Description'),
        'Vehicle engine will not start today',
      );
      await tap(tester, 'Next: Location');
      expect(draft(tester).canSubmit, isFalse);
      await unmount(tester);
    },
  );

  testWidgets(
    'Edit uses persisted request location instead of Home suggestion',
    (tester) async {
      await mount(tester);
      await capture(tester);
      Navigator.of(tester.element(find.byType(CustomerHomeScreen))).push(
        MaterialPageRoute<void>(
          builder: (_) => EditServiceRequestScreen(request: request('A')),
        ),
      );
      await tester.pumpAndSettle();
      expect(draft(tester).text.text, 'Colombo');
      expect(draft(tester).preview, isNull);
      expect(find.text(addressA), findsNothing);
      await unmount(tester);
    },
  );

  for (final expiry in [false, true]) {
    testWidgets(
      '${expiry ? '401' : 'logout'} clears location and A cannot leak to B or Provider',
      (tester) async {
        await mount(tester);
        await capture(tester);
        final old = homeProvider(tester);
        final delayed = Completer<ResolvedLocation>();
        geo.reply = () => delayed.future;
        final pending = old.capture();
        await tester.pump();
        if (expiry) {
          api.client.httpClientAdapter = ReplyAdapter(
            (_) async => ResponseBody.fromString('{}', 401),
          );
          final response = expectLater(
            api.client.get('/protected'),
            throwsA(isA<DioException>()),
          );
          await tester.pumpAndSettle();
          await response;
        } else {
          await auth.logout();
          await tester.pumpAndSettle();
        }
        expect(old.suggestion, isNull);
        authService.nextUser = AuthStub.user('B');
        await auth.login(email: 'b@example.com', password: 'password');
        await tester.pumpAndSettle();
        delayed.complete(const ResolvedLocation(formattedAddress: addressB));
        await pending;
        await tester.pumpAndSettle();
        expect(homeProvider(tester).suggestion, isNull);
        expect(find.text(addressA), findsNothing);
        await auth.logout();
        authService.nextUser = AuthStub.user('P', 'Provider');
        await auth.login(email: 'p@example.com', password: 'password');
        await tester.pumpAndSettle();
        expect(find.byType(ProviderHomeScreen), findsOneWidget);
        expect(
          tester
              .element(find.byType(ProviderHomeScreen))
              .read<CustomerLocationProvider?>(),
          isNull,
        );
        await unmount(tester);
      },
    );
  }

  test('draft suggestion expires before confirmation; manually derived source stays manual', () {
    final c = LocationSelectionController(
      gps: gps,
      geocoding: geo,
      initialSuggestion: snapshot(),
      clock: () => now,
    );
    addTearDown(c.dispose);
    now = now.add(const Duration(minutes: 15));
    c.confirm();
    expect(c.canSubmit, isFalse);
    expect(c.message, contains('expired'));
    expect(c.text.text, isEmpty);
    final manual = LocationSelectionController(
      gps: gps,
      geocoding: geo,
      initialSuggestion: snapshot(source: LocationSource.manual),
      clock: () => now,
    );
    addTearDown(manual.dispose);
    manual.confirm();
    expect(manual.source, LocationSource.manual);
  });

  for (final scale in [1.0, 2.0]) {
    testWidgets('long address and Home sections fit 320px at scale $scale', (
      tester,
    ) async {
      tester.view.physicalSize = const Size(320, 640);
      tester.view.devicePixelRatio = 1;
      tester.platformDispatcher.textScaleFactorTestValue = scale;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      addTearDown(tester.platformDispatcher.clearTextScaleFactorTestValue);
      await mount(tester);
      await capture(tester);
      expect(find.text(addressA), findsOneWidget);
      expect(tester.takeException(), isNull);
      expect(
        tester
            .renderObject<RenderParagraph>(find.text('Vehicle Assistance'))
            .didExceedMaxLines,
        isFalse,
      );
      await tester.ensureVisible(find.text('Recent Activity'));
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
      expect(find.byType(CustomerBottomNavigation).hitTestable(), findsOneWidget);
      await unmount(tester);
    });
  }
  test(
    'overlong Home address remains intact with manual fallback guidance',
    () {
      final address = List.filled(260, 'a').join();
      final c = LocationSelectionController(
        gps: gps,
        geocoding: geo,
        clock: () => now,
        initialSuggestion: LocationSuggestion(
          location: ResolvedLocation(formattedAddress: address),
          latitude: 6.9,
          longitude: 79.9,
          source: LocationSource.openStreetMap,
          capturedAt: now,
        ),
      );
      addTearDown(c.dispose);
      expect(c.preview!.formattedAddress, address);
      expect(c.message, contains('255'));
      c.confirm();
      expect(c.canSubmit, isFalse);
      expect(c.text.text, isEmpty);
    },
  );
}
