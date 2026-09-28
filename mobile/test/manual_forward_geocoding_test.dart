import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'package:mobile/core/api/api_client.dart';
import 'package:mobile/features/service_requests/models/forward_geocode_candidate.dart';
import 'package:mobile/features/service_requests/models/location_source.dart';
import 'package:mobile/features/service_requests/models/service_request_model.dart';
import 'package:mobile/features/service_requests/providers/location_selection_controller.dart';
import 'package:mobile/features/service_requests/providers/service_request_provider.dart';
import 'package:mobile/features/service_requests/screens/create_service_request_screen.dart';
import 'package:mobile/features/service_requests/screens/edit_service_request_screen.dart';
import 'package:mobile/features/service_requests/services/location_geocoding_service.dart';
import 'package:mobile/shared/theme/app_spacing.dart';
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

  Widget screen(Widget child, MockServiceRequestService service) =>
      ChangeNotifierProvider(
        create: (_) => ServiceRequestProvider(serviceRequestService: service),
        child: MaterialApp(theme: AppTheme.lightTheme, home: child),
      );

  group('ForwardGeocodeCandidate Model', () {
    test('parses json correctly with default source', () {
      final json = {
        'displayAddress': 'Independence Square, Colombo 07, Sri Lanka',
        'latitude': 6.905,
        'longitude': 79.86,
        'placeId': '101',
      };
      final candidate = ForwardGeocodeCandidate.fromJson(json);
      expect(candidate.displayAddress, 'Independence Square, Colombo 07, Sri Lanka');
      expect(candidate.latitude, 6.905);
      expect(candidate.longitude, 79.86);
      expect(candidate.placeId, '101');
      expect(candidate.source, 'OpenStreetMap');
    });

    test('serializes to json correctly', () {
      const candidate = ForwardGeocodeCandidate(
        displayAddress: 'Arcade Independence Square',
        latitude: 6.904,
        longitude: 79.861,
        placeId: '102',
        source: 'OpenStreetMap',
      );
      final json = candidate.toJson();
      expect(json['displayAddress'], 'Arcade Independence Square');
      expect(json['latitude'], 6.904);
      expect(json['longitude'], 79.861);
      expect(json['placeId'], '102');
      expect(json['source'], 'OpenStreetMap');
    });
  });

  group('LocationGeocodingService.forwardGeocode HTTP client', () {
    test('posts to /location/forward-geocode with address payload', () async {
      final dio = Dio();
      dio.interceptors.add(
        InterceptorsWrapper(
          onRequest: (options, handler) {
            expect(options.path, '/location/forward-geocode');
            expect(options.method, 'POST');
            expect(options.data, {'address': 'Independence Square'});
            handler.resolve(
              Response(
                requestOptions: options,
                statusCode: 200,
                data: [
                  {
                    'displayAddress': 'Independence Square, Colombo 07, Sri Lanka',
                    'latitude': 6.905,
                    'longitude': 79.86,
                    'placeId': '101',
                    'source': 'OpenStreetMap',
                  }
                ],
              ),
            );
          },
        ),
      );
      final client = LocationGeocodingService(apiClient: ApiClient(dio: dio));
      final results = await client.forwardGeocode('Independence Square');
      expect(results.length, 1);
      expect(results.first.displayAddress, 'Independence Square, Colombo 07, Sri Lanka');
      expect(results.first.latitude, 6.905);
      expect(results.first.longitude, 79.86);
    });

    test('handles empty results list', () async {
      final dio = Dio();
      dio.interceptors.add(
        InterceptorsWrapper(
          onRequest: (options, handler) {
            handler.resolve(
              Response(
                requestOptions: options,
                statusCode: 200,
                data: [],
              ),
            );
          },
        ),
      );
      final client = LocationGeocodingService(apiClient: ApiClient(dio: dio));
      final results = await client.forwardGeocode('NonExistentPlace 9999');
      expect(results, isEmpty);
    });
  });

  group('LocationSelectionController Manual Forward Geocoding', () {
    test('resolveAddress populates candidates without making them authoritative before confirmation', () async {
      geocoding.forwardReply = (addr) async => [
        const ForwardGeocodeCandidate(
          displayAddress: 'Candidate 1, Colombo',
          latitude: 6.91,
          longitude: 79.85,
        ),
        const ForwardGeocodeCandidate(
          displayAddress: 'Candidate 2, Kandy',
          latitude: 7.29,
          longitude: 80.63,
        ),
      ];

      selection.text.text = 'Independence Square';
      expect(selection.latitude, isNull);
      expect(selection.longitude, isNull);
      expect(selection.hasConfirmedCoordinates, isFalse);

      await selection.resolveAddress();

      expect(selection.candidates.length, 2);
      expect(selection.selectedCandidate?.displayAddress, 'Candidate 1, Colombo');
      // Must NOT be authoritative yet!
      expect(selection.latitude, isNull);
      expect(selection.longitude, isNull);
      expect(selection.source, LocationSource.manual);
      expect(selection.hasConfirmedCoordinates, isFalse);
      expect(selection.canSubmit, isFalse);

      // Select candidate 2
      selection.selectCandidate(selection.candidates[1]);
      expect(selection.selectedCandidate?.displayAddress, 'Candidate 2, Kandy');
      expect(selection.latitude, isNull);

      // Explicitly confirm candidate
      selection.confirmCandidate();
      expect(selection.latitude, 7.29);
      expect(selection.longitude, 80.63);
      expect(selection.source, LocationSource.openStreetMap);
      expect(selection.text.text, 'Candidate 2, Kandy');
      expect(selection.candidates, isEmpty);
      expect(selection.hasConfirmedCoordinates, isTrue);
      expect(selection.canSubmit, isTrue);
    });

    test('manual text modification immediately invalidates forward-geocoded coordinates', () async {
      geocoding.forwardReply = (addr) async => [
        const ForwardGeocodeCandidate(
          displayAddress: 'Galle Face Green, Colombo',
          latitude: 6.92,
          longitude: 79.84,
        ),
      ];

      selection.text.text = 'Galle Face';
      await selection.resolveAddress();
      selection.confirmCandidate();

      expect(selection.latitude, 6.92);
      expect(selection.longitude, 79.84);
      expect(selection.hasConfirmedCoordinates, isTrue);

      // Customer modifies address text
      selection.text.text = 'Kandy City Center';

      expect(selection.latitude, isNull);
      expect(selection.longitude, isNull);
      expect(selection.source, LocationSource.manual);
      expect(selection.hasConfirmedCoordinates, isFalse);
    });

    test('empty query sets error message and does not call service', () async {
      selection.text.text = '   ';
      await selection.resolveAddress();
      expect(geocoding.forwardCalls, 0);
      expect(selection.message, contains('service location'));
    });

    test('service error sets user friendly message', () async {
      geocoding.forwardReply = (addr) async => throw Exception('Service Unavailable');
      selection.text.text = 'Valid Address';
      await selection.resolveAddress();
      expect(selection.message, contains('Could not resolve'));
      expect(selection.candidates, isEmpty);
      expect(selection.hasConfirmedCoordinates, isFalse);
    });
  });

  group('CreateServiceRequestScreen Constraint 2 Verification', () {
    testWidgets(
      'tapping Next: Review with unconfirmed address triggers forward geocoding, NEVER auto-accepts, and requires explicit confirmation',
      (tester) async {
        final service = MockServiceRequestService();
        geocoding.forwardReply = (addr) async => [
          const ForwardGeocodeCandidate(
            displayAddress: 'Independence Square, Colombo 07, Sri Lanka',
            latitude: 6.905,
            longitude: 79.86,
            placeId: '101',
          ),
          const ForwardGeocodeCandidate(
            displayAddress: 'Independence Arcade, Colombo 07, Sri Lanka',
            latitude: 6.904,
            longitude: 79.861,
            placeId: '102',
          ),
        ];

        await tester.pumpWidget(
          screen(
            CreateServiceRequestScreen(
              locationService: gps,
              geocodingService: geocoding,
            ),
            service,
          ),
        );

        // Step 0: Enter details
        await tester.enterText(
          find.widgetWithText(TextFormField, 'Problem Description'),
          'Water pipe burst under kitchen sink',
        );
        await tester.ensureVisible(find.text('Next: Location'));
        await tester.tap(find.text('Next: Location'));
        await tester.pumpAndSettle();

        // Step 1: Manually enter address without tapping resolve or GPS
        await tester.enterText(
          find.widgetWithText(TextFormField, 'Location / Address'),
          'Independence Square, Colombo 07',
        );
        await tester.pumpAndSettle();

        // Tap Next: Review
        await tester.ensureVisible(find.text('Next: Review'));
        await tester.tap(find.text('Next: Review'));
        await tester.pumpAndSettle();

        // CONSTRAINT 2 VERIFICATION:
        // Forward geocoding was triggered
        expect(geocoding.forwardCalls, 1);
        expect(geocoding.forwardAddress, 'Independence Square, Colombo 07');

        // MUST NOT advance to Step 2 (Review) automatically!
        expect(find.text('Review Service Request'), findsNothing);
        expect(find.text('Matching locations (2)'), findsOneWidget);
        expect(find.text('Independence Square, Colombo 07, Sri Lanka'), findsOneWidget);
        expect(find.text('Independence Arcade, Colombo 07, Sri Lanka'), findsOneWidget);
        expect(find.byKey(const Key('use_forward_location_button')), findsOneWidget);

        // Explicitly click "Use This Location"
        await tester.tap(find.byKey(const Key('use_forward_location_button')));
        await tester.pumpAndSettle();

        // Candidate is now confirmed, candidates card dismissed
        expect(find.text('Matching locations (2)'), findsNothing);
        expect(find.text('Address resolved & coordinates confirmed'), findsOneWidget);

        // Now tap Next: Review again -> should advance to Review!
        await tester.ensureVisible(find.text('Next: Review'));
        await tester.tap(find.text('Next: Review'));
        await tester.pumpAndSettle();

        expect(find.text('Review Service Request'), findsOneWidget);
        expect(find.text('Independence Square, Colombo 07, Sri Lanka'), findsOneWidget);
        expect(find.text('Coordinates resolved & confirmed'), findsOneWidget);
        expect(find.text('\u00a9 OpenStreetMap contributors'), findsOneWidget);

        // Submit request
        await tester.ensureVisible(find.text('Submit Request'));
        await tester.tap(find.text('Submit Request'));
        await tester.pumpAndSettle();

        // Verify authoritative payload
        final dto = service.lastCreateDto!;
        expect(dto.locationText, 'Independence Square, Colombo 07, Sri Lanka');
        expect(dto.latitude, 6.905);
        expect(dto.longitude, 79.86);
        expect(dto.toJson()['locationSource'], 'OpenStreetMap');
      },
    );

    testWidgets(
      'user can resolve address via Resolve Address button and pick alternative candidate',
      (tester) async {
        final service = MockServiceRequestService();
        geocoding.forwardReply = (addr) async => [
          const ForwardGeocodeCandidate(
            displayAddress: 'Location Option A',
            latitude: 6.91,
            longitude: 79.85,
            placeId: 'opt-a',
          ),
          const ForwardGeocodeCandidate(
            displayAddress: 'Location Option B',
            latitude: 6.92,
            longitude: 79.86,
            placeId: 'opt-b',
          ),
        ];

        await tester.pumpWidget(
          screen(
            CreateServiceRequestScreen(
              locationService: gps,
              geocodingService: geocoding,
            ),
            service,
          ),
        );

        // Step 0
        await tester.enterText(
          find.widgetWithText(TextFormField, 'Problem Description'),
          'Electrical short circuit in dining hall',
        );
        await tester.ensureVisible(find.text('Next: Location'));
        await tester.tap(find.text('Next: Location'));
        await tester.pumpAndSettle();

        // Step 1: Type address
        await tester.enterText(
          find.widgetWithText(TextFormField, 'Location / Address'),
          'Option Search',
        );
        await tester.pumpAndSettle();

        // Resolve Address button is visible
        expect(find.byKey(const Key('resolve_address_button')), findsOneWidget);
        await tester.tap(find.byKey(const Key('resolve_address_button')));
        await tester.pumpAndSettle();

        expect(geocoding.forwardCalls, 1);
        expect(find.text('Location Option A'), findsOneWidget);
        expect(find.text('Location Option B'), findsOneWidget);

        // Tap Option B tile to select it
        await tester.tap(find.byKey(const Key('candidate_tile_opt-b')));
        await tester.pumpAndSettle();

        // Confirm Option B
        await tester.tap(find.byKey(const Key('use_forward_location_button')));
        await tester.pumpAndSettle();

        // Advance to review
        await tester.ensureVisible(find.text('Next: Review'));
        await tester.tap(find.text('Next: Review'));
        await tester.pumpAndSettle();

        expect(find.text('Review Service Request'), findsOneWidget);
        expect(find.text('Location Option B'), findsOneWidget);

        await tester.ensureVisible(find.text('Submit Request'));
        await tester.tap(find.text('Submit Request'));
        await tester.pumpAndSettle();

        final dto = service.lastCreateDto!;
        expect(dto.locationText, 'Location Option B');
        expect(dto.latitude, 6.92);
        expect(dto.longitude, 79.86);
      },
    );
  });

  group('EditServiceRequestScreen Location Resolution', () {
    testWidgets(
      'editing address text clears coordinates and requires resolution',
      (tester) async {
        final request = ServiceRequestModel.fromJson({
          'serviceRequestId': 'req-edit-1',
          'locationText': 'Original Road, Colombo',
          'description': 'Original description here',
          'latitude': 6.91,
          'longitude': 79.85,
          'locationSource': 'OpenStreetMap',
        });
        final service = MockServiceRequestService()..mockRequests = [request];
        geocoding.forwardReply = (addr) async => [
          const ForwardGeocodeCandidate(
            displayAddress: 'Updated Road, Kandy',
            latitude: 7.29,
            longitude: 80.63,
            placeId: 'knd-1',
          ),
        ];

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

        // Modify address text
        await tester.enterText(
          find.widgetWithText(TextFormField, 'Location / Address'),
          'Updated Road, Kandy',
        );
        await tester.pumpAndSettle();

        // Tap Save Changes
        await tester.ensureVisible(find.text('Save Changes'));
        await tester.tap(find.text('Save Changes'));
        await tester.pumpAndSettle();

        // Triggers resolution and displays candidate
        expect(geocoding.forwardCalls, 1);
        expect(find.text('Resolved service location'), findsOneWidget);
        expect(find.text('Updated Road, Kandy'), findsWidgets);

        // Confirm location
        await tester.tap(find.byKey(const Key('use_forward_location_button')));
        await tester.pumpAndSettle();

        // Now tap Save Changes again
        await tester.ensureVisible(find.text('Save Changes'));
        await tester.tap(find.text('Save Changes'));
        await tester.pumpAndSettle();

        final updatedDto = service.lastUpdateDto!;
        expect(updatedDto.locationText, 'Updated Road, Kandy');
        expect(updatedDto.latitude, 7.29);
        expect(updatedDto.longitude, 80.63);
        expect(updatedDto.locationSource, LocationSource.openStreetMap);
      },
    );
  });

  group('Manual Location Field Layout and Spacing Polish', () {
    testWidgets(
      'manual address field remains visible, has clear vertical separation, and does not overlap candidate card',
      (tester) async {
        tester.view.physicalSize = const Size(412, 900);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        final service = MockServiceRequestService();
        geocoding.forwardReply = (addr) async => [
          const ForwardGeocodeCandidate(
            displayAddress: 'Candidate 1, Colombo',
            latitude: 6.91,
            longitude: 79.85,
            placeId: 'cand-1',
          ),
          const ForwardGeocodeCandidate(
            displayAddress: 'Candidate 2, Colombo',
            latitude: 6.92,
            longitude: 79.86,
            placeId: 'cand-2',
          ),
        ];

        await tester.pumpWidget(
          screen(
            CreateServiceRequestScreen(
              locationService: gps,
              geocodingService: geocoding,
            ),
            service,
          ),
        );

        // Step 0: Fill problem description
        await tester.enterText(
          find.widgetWithText(TextFormField, 'Problem Description'),
          'Water leakage in apartment kitchen',
        );
        await tester.ensureVisible(find.text('Next: Location'));
        await tester.tap(find.text('Next: Location'));
        await tester.pumpAndSettle();

        // Step 1: Type manual address
        final addressFieldFinder = find.widgetWithText(TextFormField, 'Location / Address');
        expect(addressFieldFinder, findsOneWidget);
        await tester.enterText(addressFieldFinder, 'colombo');
        await tester.pumpAndSettle();

        // Resolve address
        final resolveBtn = find.byKey(const Key('resolve_address_button'));
        expect(resolveBtn, findsOneWidget);
        await tester.tap(resolveBtn);
        await tester.pumpAndSettle();

        // Verify Candidate Card and Address Field are both visible
        final candidateCardFinder = find.byKey(const Key('forward_candidates_card'));
        expect(candidateCardFinder, findsOneWidget);
        expect(addressFieldFinder, findsOneWidget);

        // Geometry checks: candidate card is above address field with clear gap
        final candidateCardRect = tester.getRect(candidateCardFinder);
        final addressFieldRect = tester.getRect(addressFieldFinder);

        expect(candidateCardRect.bottom, lessThanOrEqualTo(addressFieldRect.top));
        final gap = addressFieldRect.top - candidateCardRect.bottom;
        expect(gap, greaterThanOrEqualTo(AppSpacing.md));

        // Candidate selection works
        await tester.tap(find.byKey(const Key('candidate_tile_cand-2')));
        await tester.pumpAndSettle();

        // Explicit "Use This Location" tap confirms
        await tester.tap(find.byKey(const Key('use_forward_location_button')));
        await tester.pumpAndSettle();

        expect(find.byKey(const Key('forward_candidates_card')), findsNothing);
        expect(find.text('Address resolved & coordinates confirmed'), findsOneWidget);
        expect(tester.takeException(), isNull);
      },
    );

    testWidgets(
      'Cancel / Keep Editing dismisses candidates and restores address field state',
      (tester) async {
        tester.view.physicalSize = const Size(412, 900);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        final service = MockServiceRequestService();
        geocoding.forwardReply = (addr) async => [
          const ForwardGeocodeCandidate(
            displayAddress: 'Candidate 1, Colombo',
            latitude: 6.91,
            longitude: 79.85,
            placeId: 'cand-1',
          ),
        ];

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
          'Sink faucet replacement needed',
        );
        await tester.ensureVisible(find.text('Next: Location'));
        await tester.tap(find.text('Next: Location'));
        await tester.pumpAndSettle();

        await tester.enterText(
          find.widgetWithText(TextFormField, 'Location / Address'),
          'colombo 03',
        );
        await tester.pumpAndSettle();

        await tester.tap(find.byKey(const Key('resolve_address_button')));
        await tester.pumpAndSettle();

        expect(find.byKey(const Key('forward_candidates_card')), findsOneWidget);

        // Tap Cancel / Keep Editing
        await tester.tap(find.byKey(const Key('cancel_forward_candidates_button')));
        await tester.pumpAndSettle();

        // Candidates card dismissed, address field still contains text, resolve button back
        expect(find.byKey(const Key('forward_candidates_card')), findsNothing);
        expect(find.text('colombo 03'), findsOneWidget);
        expect(find.byKey(const Key('resolve_address_button')), findsOneWidget);
        expect(tester.takeException(), isNull);
      },
    );

    for (final width in [320.0, 412.0]) {
      for (final scale in [1.0, 2.0]) {
        testWidgets(
          'Location selection with candidate card renders cleanly at $width scale $scale',
          (tester) async {
            tester.view.physicalSize = Size(width, 1000);
            tester.view.devicePixelRatio = 1;
            tester.platformDispatcher.textScaleFactorTestValue = scale;
            addTearDown(tester.view.resetPhysicalSize);
            addTearDown(tester.view.resetDevicePixelRatio);
            addTearDown(tester.platformDispatcher.clearTextScaleFactorTestValue);

            final service = MockServiceRequestService();
            geocoding.forwardReply = (addr) async => [
              const ForwardGeocodeCandidate(
                displayAddress: 'Very Long Candidate Address 123, Sector 4, Colombo 00700, Western Province, Sri Lanka',
                latitude: 6.91,
                longitude: 79.85,
                placeId: 'long-cand',
              ),
            ];

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
              'General maintenance required',
            );
            await tester.ensureVisible(find.text('Next: Location'));
            await tester.tap(find.text('Next: Location'));
            await tester.pumpAndSettle();

            await tester.enterText(
              find.widgetWithText(TextFormField, 'Location / Address'),
              'colombo',
            );
            await tester.pumpAndSettle();

            await tester.tap(find.byKey(const Key('resolve_address_button')));
            await tester.pumpAndSettle();

            expect(find.byKey(const Key('forward_candidates_card')), findsOneWidget);
            expect(find.widgetWithText(TextFormField, 'Location / Address'), findsOneWidget);
            expect(tester.takeException(), isNull);
          },
        );
      }
    }
  });
}
