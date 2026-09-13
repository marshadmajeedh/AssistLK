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
import 'session_foundation_test.dart' show AuthStub;

void main() {
  late AuthProvider auth;
  late AuthStub authService;
  late PhotoApi api;
  late FakePhotoPicker picker;

  setUp(() {
    final tokens = MemoryTokens();
    authService = AuthStub(ApiClient(tokenStorage: tokens));
    auth = AuthProvider(authService: authService, tokenStorage: tokens);
    api = PhotoApi();
    picker = FakePhotoPicker()..next = photo(1);
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
        child: AssistLKApp(serviceRequestService: api),
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
    await tap(tester, 'Next: Location');
    await tester.enterText(
      find.widgetWithText(TextFormField, 'Location / Address'),
      'Colombo',
    );
    await tap(tester, 'Next: Review');
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

  testWidgets(
    'shell wizard retains draft photos through Back and opens uploaded detail',
    (tester) async {
      await mountAndReview(tester);
      await tap(tester, 'Back');
      await tap(tester, 'Back');
      expect(find.byType(PrivatePhotoImage), findsOneWidget);
      await tap(tester, 'Next: Location');
      await tap(tester, 'Next: Review');
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
