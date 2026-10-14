import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:mobile/app/app.dart';
import 'package:mobile/app/customer_app_shell.dart';
import 'package:mobile/core/api/api_client.dart';
import 'package:mobile/features/auth/providers/auth_provider.dart';
import 'package:mobile/features/providers/provider_home_screen.dart';
import 'package:mobile/features/service_requests/models/service_request_attachment.dart';
import 'package:mobile/features/service_requests/navigation/open_create_service_request.dart';
import 'package:mobile/features/service_requests/providers/service_request_provider.dart';
import 'package:mobile/features/service_requests/screens/create_service_request_screen.dart';
import 'package:mobile/features/service_requests/screens/customer_home_screen.dart';
import 'package:mobile/features/service_requests/screens/service_request_detail_screen.dart';
import 'package:mobile/features/service_requests/services/problem_image_picker.dart';
import 'package:mobile/features/service_requests/widgets/problem_photos.dart';

import 'mocks/fake_problem_photos.dart';
import 'mocks/mock_location_geocoding_service.dart';
import 'session_foundation_test.dart' show AuthStub;

/**
 * TEST SUITE: Shell Problem Photos and Multi-Step Wizard Lifecycle Tests
 *
 * 1. What this test suite tests:
 *    - Verifies draft photo persistence through back-and-forth wizard navigation in `CreateServiceRequestScreen`.
 *    - Verifies uploaded photo attachment display in `ServiceRequestDetailScreen` (`RequestProblemPhotos`).
 *    - Verifies safety under late photo uploads: if the user logs out and switches accounts (to another Customer or a Provider),
 *      late asynchronous photo upload responses cannot restore old routes, inject data into the new session, or leak images across accounts.
 * 2. Why these tests are needed:
 *    - Service request creation is a multi-step guided wizard with asynchronous photo uploads.
 *      Draft photos must survive navigation steps (Back, Forward), and race conditions during late network resolution
 *      on logout/login must be securely sanitized to protect customer data.
 * 3. What is real and what is mocked:
 *    - Real `AssistLKApp` widget tree, `CustomerAppShell`, `CreateServiceRequestScreen`, `ServiceRequestDetailScreen`.
 *    - Mocked: `AuthStub` for login/logout user switching, `PhotoApi` for mock photo upload endpoints, `FakePhotoPicker` for file picking, `MockLocationGeocodingService`.
 * 4. Prerequisites: None.
 * 5. PowerShell command to run this suite from repository root:
 *    Push-Location mobile; flutter test test/shell_problem_photos_test.dart; Pop-Location
 */
void main() {
  late AuthProvider auth;
  late AuthStub authService;
  late PhotoApi api;
  late FakePhotoPicker picker;
  late MockLocationGeocodingService geocoding;

  setUp(() {
    final tokens = MemoryTokens();
    authService = AuthStub(ApiClient(tokenStorage: tokens));
    auth = AuthProvider(authService: authService, tokenStorage: tokens);
    api = PhotoApi();
    picker = FakePhotoPicker()..next = photo(1);
    geocoding = MockLocationGeocodingService();
  });
  tearDown(() => auth.dispose());

  Future<void> tap(WidgetTester tester, String label) async {
    final target = find.text(label).last;
    await tester.ensureVisible(target);
    await tester.tap(target);
    await tester.pumpAndSettle();
  }

  Future<void> mountAndReview(WidgetTester tester) async {
    await auth.login(email: 'a@example.com', password: 'password');
    await tester.pumpWidget(
      MultiProvider(
        providers: [
          ChangeNotifierProvider<AuthProvider>.value(value: auth),
          Provider<ProblemImagePicker>.value(value: picker),
        ],
        child: AssistLKApp(
          serviceRequestService: api,
          geocodingService: geocoding,
        ),
      ),
    );
    await tester.pumpAndSettle();
    openCreateServiceRequest(
      tester.element(find.byType(CustomerHomeScreen)),
      categoryHint: 'Plumbing',
    );
    await tester.pumpAndSettle();
    await tap(tester, 'Add Photo');
    await tap(tester, 'Choose from Gallery');
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Problem Description'),
      'Water is leaking under the sink',
    );
    await tap(tester, 'Continue to Location');
    await tap(tester, 'Enter Manually');
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Location / Address'),
      'Colombo',
    );
    await tap(tester, 'Review Request');
    if (find.text('Use This Location').evaluate().isNotEmpty) {
      await tap(tester, 'Use This Location');
      await tap(tester, 'Review Request');
    }
    expect(find.byType(PrivatePhotoImage), findsOneWidget);
    expect(find.byKey(const Key('wizard_actions')), findsOneWidget);
    expect(
      tester
          .widget<CreateServiceRequestScreen>(
            find.byType(CreateServiceRequestScreen),
          )
          .initialCategoryPreference,
      'Plumbing',
    );
  }

  /**
   * Purpose: What this specific test checks
   * - Verifies that navigating backward ('Back', 'Back') to Step 1 preserves selected draft photos, and proceeding to submit completes creation, photo upload, and opens the detail screen with `RequestProblemPhotos`.
   *
   * Why: Why this scenario matters
   * - Prevents frustrating data loss if a customer goes back to revise their description or change category.
   *
   * Scenario: Input or condition being tested
   * - Add photo, proceed to review, tap Back twice, verify photo remains, proceed and Submit.
   *
   * Pass condition: What result or assertion makes it pass
   * - `PrivatePhotoImage` remains rendered, detail screen opens with `RequestProblemPhotos`, API records create and upload events.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/shell_problem_photos_test.dart --plain-name "shell wizard retains draft photos through Back and opens uploaded detail"; Pop-Location
   */
  testWidgets(
    'shell wizard retains draft photos through Back and opens uploaded detail',
    (tester) async {
      await mountAndReview(tester);
      await tap(tester, 'Back');
      await tap(tester, 'Back');
      expect(find.byType(PrivatePhotoImage), findsOneWidget);
      await tap(tester, 'Continue to Location');
      await tap(tester, 'Review Request');
      await tap(tester, 'Submit Request');
      expect(find.byType(ServiceRequestDetailScreen), findsOneWidget);
      expect(find.byType(RequestProblemPhotos), findsOneWidget);
      expect(api.events.where((e) => e == 'create'), hasLength(1));
      expect(api.events.where((e) => e.startsWith('upload:')), hasLength(1));
      expect(api.events, contains('content:attachment-1'));
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox.shrink());
    },
  );

  /**
   * Parameterized Account Switching Safety Tests
   * Note: Parameterized across user roles ['Customer', 'Provider']. The test runner executes both cases.
   *
   * Purpose: What this specific test checks
   * - Verifies that late completing photo uploads after account switching cannot pop up old detail screens or leak data to the new user.
   *
   * Why: Why this scenario matters
   * - Prevents cross-account session pollution and security breaches where User B sees User A's uploaded pictures.
   *
   * Scenario: Input or condition being tested
   * - Submit request with pending photo upload, log out mid-upload, log in as User B (as Customer or Provider), complete photo upload afterwards.
   *
   * Pass condition: What result or assertion makes it pass
   * - Detail screen and draft screen do not appear, old requests list is cleared, controller is deactivated (`photos.active == false`), new shell mounts cleanly.
   *   (Note: Provider role switch test exhibits pumpAndSettle timeout on ProviderHomeScreen animation in test harness).
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/shell_problem_photos_test.dart --plain-name "late photo upload cannot restore old routes or state after switching to Customer"; Pop-Location
   */
  for (final role in ['Customer', 'Provider']) {
    testWidgets(
      'late photo upload cannot restore old routes or state after switching to $role',
      (tester) async {
        await mountAndReview(tester);
        final oldRequests = tester
            .element(find.byType(CreateServiceRequestScreen))
            .read<ServiceRequestProvider>();
        final photos = tester
            .widget<DraftProblemPhotos>(find.byType(DraftProblemPhotos))
            .controller;
        api.uploadPending = Completer<ServiceRequestAttachment>();
        await tester.tap(find.text('Submit Request'));
        await tester.pump();
        expect(photos.busy, isTrue);
        await auth.logout();
        authService.nextUser = AuthStub.user('B', role);
        await auth.login(email: 'b@example.com', password: 'password');
        await tester.pumpAndSettle();
        expect(photos.active, isFalse);
        expect(oldRequests.requests, isEmpty);
        api.uploadPending!.complete(attachment(1));
        await tester.pumpAndSettle();
        expect(find.byType(ServiceRequestDetailScreen), findsNothing);
        expect(find.byType(CreateServiceRequestScreen), findsNothing);
        expect(find.byType(PrivatePhotoImage), findsNothing);
        expect(auth.user!.userId, 'B');
        if (role == 'Customer') {
          expect(find.byType(CustomerAppShell), findsOneWidget);
          final current = tester
              .element(find.byType(CustomerHomeScreen))
              .read<ServiceRequestProvider>();
          expect(identical(current, oldRequests), isFalse);
        } else {
          expect(find.byType(ProviderHomeScreen), findsOneWidget);
          expect(
            tester
                .element(find.byType(ProviderHomeScreen))
                .read<ServiceRequestProvider?>(),
            isNull,
          );
        }
        expect(tester.takeException(), isNull);
        await tester.pumpWidget(const SizedBox.shrink());
      },
    );
  }
}
