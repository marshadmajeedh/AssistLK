import 'package:url_launcher/link.dart';
import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:mobile/core/api/api_client.dart';
import 'package:mobile/features/service_requests/models/location_source.dart';
import 'package:mobile/features/service_requests/models/resolved_location.dart';
import 'package:mobile/features/service_requests/models/service_request_model.dart';
import 'package:mobile/features/service_requests/providers/location_selection_controller.dart';
import 'package:mobile/features/service_requests/providers/service_request_provider.dart';
import 'package:mobile/features/service_requests/screens/create_service_request_screen.dart';
import 'package:mobile/features/service_requests/screens/edit_service_request_screen.dart';
import 'package:mobile/features/service_requests/screens/service_request_detail_screen.dart';
import 'package:mobile/features/service_requests/services/location_geocoding_service.dart';
import 'package:mobile/features/service_requests/services/location_service.dart';
import 'package:mobile/features/service_requests/widgets/location_selection.dart';
import 'package:mobile/shared/theme/app_theme.dart';

import 'mocks/mock_location_geocoding_service.dart';
import 'mocks/mock_location_service.dart';
import 'service_request_screens_test.dart' show MockServiceRequestService;

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late MockLocationService gps;
  late MockLocationGeocodingService geocoding;
  late LocationSelectionController selection;
  setUp(() {
    gps = MockLocationService();
    geocoding = MockLocationGeocodingService();
    selection = LocationSelectionController(gps: gps, geocoding: geocoding);
  });
  tearDown(() => selection.dispose());

  Widget locationWidget() => MaterialApp(
    theme: AppTheme.lightTheme,
    home: Scaffold(
      body: SingleChildScrollView(
        child: Form(child: LocationSelection(controller: selection)),
      ),
    ),
  );

  for (final width in [320.0, 600.0]) {
    testWidgets('preview polish and responsive actions at width $width', (tester) async {
      tester.view.physicalSize = Size(width, 1000);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      gps.customResult = const LocationResult(status: LocationAccessStatus.granted,
        coordinates: LocationCoordinates(latitude: 6, longitude: 79, accuracy: 40));
      await selection.capture();
      await tester.pumpWidget(locationWidget());
      expect(find.text('Example Road, Kotte'), findsOneWidget);
      expect(find.text('Accuracy approximately 40 m'), findsOneWidget);
      expect(find.text('Location captured'), findsNothing);
      expect(find.text('GPS location captured'), findsNothing);
      expect(find.text('\u00a9 OpenStreetMap contributors'), findsOneWidget);
      expect(find.textContaining('Open Database License'), findsNothing);
      expect(find.textContaining('https://'), findsNothing);
      final link = tester.widget<Link>(find.byType(Link));
      expect(link.uri.toString(), 'https://www.openstreetmap.org/copyright');
      final refresh = find.widgetWithText(OutlinedButton, 'Refresh Location');
      final manual = find.widgetWithText(OutlinedButton, 'Enter Manually');
      final refreshRect = tester.getRect(refresh);
      final manualRect = tester.getRect(manual);
      if (width == 320) {
        expect(manualRect.top, greaterThan(refreshRect.bottom));
        expect(manualRect.width, refreshRect.width);
      } else {
        expect(manualRect.top, refreshRect.top);
        expect(manualRect.left, greaterThan(refreshRect.right));
      }
      expect(refreshRect.height, greaterThanOrEqualTo(48));
      final confirm = find.widgetWithText(ElevatedButton, 'Use This Location');
      expect(tester.getSize(confirm).width, closeTo(width - 32, 1));
      expect(tester.getSize(confirm).height, greaterThanOrEqualTo(48));
      expect(tester.widget<Text>(find.text('Use This Location')).maxLines, 1);
      expect(tester.takeException(), isNull);
      await tester.ensureVisible(refresh);
      await tester.tap(refresh);
      await tester.pumpAndSettle();
      expect(geocoding.calls, 2);
      await tester.ensureVisible(confirm);
      await tester.tap(confirm);
      await tester.pumpAndSettle();
      expect(selection.source, LocationSource.openStreetMap);
      expect(selection.latitude, 6);
      await tester.ensureVisible(manual);
      await tester.tap(manual);
      await tester.pumpAndSettle();
      expect(selection.source, LocationSource.manual);
      expect(selection.needsGpsChoice, isTrue);
      expect(find.text('\u00a9 OpenStreetMap contributors'), findsNothing);
    });
  }

  test('GPS calls geocoding and requires confirmation before selecting text and source', () async {
    await selection.capture();
    expect(geocoding.calls, 1);
    expect(geocoding.latitude, 6.9271);
    expect(geocoding.longitude, 79.8612);
    expect(selection.canSubmit, isFalse);
    expect(selection.text.text, isEmpty);
    selection.confirm();
    expect(selection.text.text, 'Example Road, Kotte');
    expect(selection.latitude, 6.9271);
    expect(selection.longitude, 79.8612);
    expect(selection.source, LocationSource.openStreetMap);
    expect(selection.validate(), isNull);
  });

  for (final status in [
    LocationAccessStatus.denied,
    LocationAccessStatus.deniedForever,
    LocationAccessStatus.servicesDisabled,
    LocationAccessStatus.timeout,
  ]) {
    test(
      '$status leaves manual fallback usable and never calls geocoding',
      () async {
        gps.customResult = LocationResult(
          status: status,
          message: 'Use manual entry',
        );
        await selection.capture();
        expect(geocoding.calls, 0);
        expect(selection.message, 'Use manual entry');
        selection.enterManually();
        selection.text.text = 'Another service address';
        expect(selection.source, LocationSource.manual);
        expect(selection.validate(), isNull);
      },
    );
  }

  for (final code in [404, 503]) {
    test(
      'geocoding $code retains device point and offers a safe manual choice',
      () async {
        geocoding.reply = () async => throw DioException(
          requestOptions: RequestOptions(),
          response: Response(
            requestOptions: RequestOptions(),
            statusCode: code,
          ),
        );
        await selection.capture();
        expect(selection.message, contains('manually'));
        expect(selection.latitude, 6.9271);
        selection.enterManually();
        selection.text.text = 'Manual address';
        expect(selection.canSubmit, isFalse);
        selection.keepGps();
        expect(selection.validate(), isNull);
        expect(selection.source, LocationSource.manual);
      },
    );
  }

  test('refresh ignores an older response completing last', () async {
    final first = Completer<ResolvedLocation>();
    geocoding.reply = () => first.future;
    final old = selection.capture();
    await Future<void>.delayed(Duration.zero);
    geocoding.reply = () async =>
        const ResolvedLocation(formattedAddress: 'New address');
    gps.customResult = const LocationResult(
      status: LocationAccessStatus.granted,
      coordinates: LocationCoordinates(latitude: 0, longitude: 1, accuracy: 12),
    );
    await selection.capture();
    first.complete(const ResolvedLocation(formattedAddress: 'Old address'));
    await old;
    selection.confirm();
    expect(selection.text.text, 'New address');
    expect(selection.latitude, 0);
    expect(selection.longitude, 1);
    expect(selection.accuracy, 12);
  });

  for (final action in ['manual', 'edit', 'navigate']) {
    test('$action invalidates in-flight response', () async {
      final result = Completer<ResolvedLocation>();
      geocoding.reply = () => result.future;
      final pending = selection.capture();
      await Future<void>.delayed(Duration.zero);
      if (action == 'manual') selection.enterManually();
      if (action == 'edit') selection.text.text = 'My address';
      if (action == 'navigate') selection.cancelPending();
      result.complete(
        const ResolvedLocation(formattedAddress: 'Stale address'),
      );
      await pending;
      expect(selection.preview, isNull);
      expect(selection.text.text, isNot('Stale address'));
      expect(selection.busy, isFalse);
    });
  }

  test('each manual edit invalidates the previous keep decision', () async {
    await selection.capture();
    selection.confirm();
    selection.text.text = 'Edited address';
    expect(selection.canSubmit, isFalse);
    selection.keepGps();
    expect(selection.canSubmit, isTrue);
    expect(selection.source, LocationSource.openStreetMap);
    selection.text.text = 'Different address';
    expect(selection.canSubmit, isFalse);
    selection.removeGps();
    expect(selection.latitude, isNull);
    expect(selection.longitude, isNull);
    expect(selection.source, LocationSource.manual);
    expect(selection.text.text, 'Different address');
    expect(selection.validate(), isNull);
  });

  test('overlong result is never silently truncated or confirmed', () async {
    geocoding.reply = () async => ResolvedLocation(formattedAddress: 'x' * 256);
    await selection.capture();
    selection.confirm();
    expect(selection.preview!.formattedAddress.length, 256);
    expect(selection.text.text, isEmpty);
    expect(selection.message, contains('255'));
    selection.enterManually();
    selection.text.text = 'Short manual address';
    selection.removeGps();
    expect(selection.validate(), isNull);
  });

  test(
    'legacy response defaults Manual and explicit OpenStreetMap round trips',
    () {
      expect(
        ServiceRequestModel.fromJson({}).locationSource,
        LocationSource.manual,
      );
      final model = ServiceRequestModel.fromJson({
        'locationSource': 'OpenStreetMap',
        'latitude': 0,
        'longitude': 1,
      });
      expect(model.toJson()['locationSource'], 'OpenStreetMap');
      expect(
        model.copyWith(description: 'Edited').locationSource,
        LocationSource.openStreetMap,
      );
      expect(model.latitude, 0);
    },
  );

  test('preview model accepts missing optional fields', () {
    final result = ResolvedLocation.fromJson({'formattedAddress': 'Area only'});
    expect(result.street, isNull);
    expect(result.placeId, isNull);
    expect(result.resolutionLevel, isNull);
  });

  test(
    'client posts correct endpoint and coordinates through shared API client',
    () async {
      final dio = Dio();
      dio.interceptors.add(
        InterceptorsWrapper(
          onRequest: (options, handler) {
            expect(options.path, '/location/reverse-geocode');
            expect(options.method, 'POST');
            expect(options.data, {'latitude': 0.0, 'longitude': 79.0});
            handler.resolve(
              Response(
                requestOptions: options,
                statusCode: 200,
                data: {'formattedAddress': 'Area', 'country': 'Sri Lanka'},
              ),
            );
          },
        ),
      );
      final client = LocationGeocodingService(apiClient: ApiClient(dio: dio));
      final result = await client.reverseGeocode(0, 79);
      expect(result.formattedAddress, 'Area');
      expect(result.country, 'Sri Lanka');
    },
  );

  for (final status in [404, 503]) {
    test('client preserves HTTP $status for fallback handling', () async {
      final dio = Dio();
      dio.interceptors.add(
        InterceptorsWrapper(
          onRequest: (options, handler) => handler.reject(
            DioException(
              requestOptions: options,
              response: Response(requestOptions: options, statusCode: status),
            ),
          ),
        ),
      );
      final client = LocationGeocodingService(apiClient: ApiClient(dio: dio));
      await expectLater(
        client.reverseGeocode(0, 0),
        throwsA(
          isA<DioException>().having(
            (e) => e.response?.statusCode,
            'status',
            status,
          ),
        ),
      );
    });
  }

  testWidgets(
    'GPS-first controls and manual input appear without typing an address',
    (tester) async {
      await tester.pumpWidget(locationWidget());
      expect(find.text('Use Current Location'), findsOneWidget);
      expect(find.text('Enter Manually'), findsOneWidget);
      expect(find.text('\u00a9 OpenStreetMap contributors'), findsNothing);
      gps.customResult = const LocationResult(
        status: LocationAccessStatus.granted,
        coordinates: LocationCoordinates(
          latitude: 6,
          longitude: 79,
          accuracy: 12,
        ),
      );
      await tester.tap(find.text('Use Current Location'));
      await tester.pumpAndSettle();
      expect(find.text('Example Road, Kotte'), findsOneWidget);
      expect(find.text('\u00a9 OpenStreetMap contributors'), findsOneWidget);
      expect(find.text('Accuracy approximately 12 m'), findsOneWidget);
      await tester.tap(find.text('Use This Location'));
      await tester.pumpAndSettle();
      expect(selection.source, LocationSource.openStreetMap);
      expect(find.text('\u00a9 OpenStreetMap contributors'), findsOneWidget);
      expect(find.textContaining('6.000'), findsNothing);
    },
  );

  testWidgets('narrow preview remains readable and has no overflow', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(320, 800);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await selection.capture();
    await tester.pumpWidget(locationWidget());
    expect(find.text('\u00a9 OpenStreetMap contributors'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets(
    'overlong preview disables confirmation and keeps manual action',
    (tester) async {
      geocoding.reply = () async =>
          ResolvedLocation(formattedAddress: 'Long address ' * 30);
      await selection.capture();
      await tester.pumpWidget(locationWidget());
      final button = tester.widget<ElevatedButton>(
        find.widgetWithText(ElevatedButton, 'Use This Location'),
      );
      expect(button.onPressed, isNull);
      expect(find.text('Enter Manually'), findsOneWidget);
      expect(find.textContaining('exceeds 255'), findsOneWidget);
    },
  );

  Widget screen(Widget child, MockServiceRequestService service) =>
      ChangeNotifierProvider(
        create: (_) => ServiceRequestProvider(serviceRequestService: service),
        child: MaterialApp(theme: AppTheme.lightTheme, home: child),
      );

  testWidgets(
    'create confirmation submits address coordinates and OpenStreetMap source',
    (tester) async {
      final service = MockServiceRequestService();
      await tester.pumpWidget(
        screen(
          CreateServiceRequestScreen(
            locationService: gps,
            geocodingService: geocoding,
          ),
          service,
        ),
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Problem Description'),
        'Water leaking beneath kitchen sink',
      );
      await tester.tap(find.text('Next: Location'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Use Current Location'));
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.text('Use This Location'));
      await tester.tap(find.text('Use This Location'));
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.text('Next: Review'));
      await tester.tap(find.text('Next: Review'));
      await tester.pumpAndSettle();
      expect(find.text('\u00a9 OpenStreetMap contributors'), findsOneWidget);
      await tester.ensureVisible(find.text('Submit Request'));
      await tester.tap(find.text('Submit Request'));
      await tester.pumpAndSettle();
      final dto = service.lastCreateDto!;
      expect(dto.locationText, 'Example Road, Kotte');
      expect(dto.latitude, 6.9271);
      expect(dto.longitude, 79.8612);
      expect(dto.toJson()['locationSource'], 'OpenStreetMap');
    },
  );

  for (final osm in [false, true]) {
    testWidgets(
      'detail and edit attribution reflect source OpenStreetMap=$osm',
      (tester) async {
        final request = ServiceRequestModel.fromJson({
          'serviceRequestId': 'req-1',
          'locationText': 'Example Road, Kotte',
          'description': 'Water leaking beneath kitchen sink',
          'latitude': 6.905,
          'longitude': 79.9195,
          'locationSource': osm ? 'OpenStreetMap' : 'Manual',
        });
        final service = MockServiceRequestService()..mockRequests = [request];
        await tester.pumpWidget(
          screen(ServiceRequestDetailScreen(requestId: 'req-1'), service),
        );
        await tester.pumpAndSettle();
        expect(find.text('Example Road, Kotte'), findsOneWidget);
        expect(
          find.text('\u00a9 OpenStreetMap contributors'),
          osm ? findsOneWidget : findsNothing,
        );
        expect(find.text('GPS location captured'), findsOneWidget);
        expect(find.textContaining('79.9195'), findsNothing);
        await tester.pumpWidget(
          screen(
            EditServiceRequestScreen(
              request: request,
              locationService: gps,
              geocodingService: geocoding,
            ),
            service,
          ),
        );
        await tester.pumpAndSettle();
        expect(
          find.text('\u00a9 OpenStreetMap contributors'),
          osm ? findsOneWidget : findsNothing,
        );
      },
    );
  }
}
