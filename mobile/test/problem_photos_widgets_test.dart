import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:mobile/features/auth/providers/auth_provider.dart';
import 'package:mobile/features/service_requests/models/problem_photo.dart';
import 'package:mobile/features/service_requests/models/service_request_status.dart';
import 'package:mobile/features/service_requests/providers/problem_photos_controller.dart';
import 'package:mobile/features/service_requests/providers/service_request_provider.dart';
import 'package:mobile/features/service_requests/screens/create_service_request_screen.dart';
import 'package:mobile/features/service_requests/screens/service_request_detail_screen.dart';
import 'package:mobile/features/service_requests/widgets/problem_photos.dart';
import 'package:mobile/shared/theme/app_theme.dart';

import 'mocks/fake_problem_photos.dart';

void main() {
  late PhotoApi api;
  late FakePhotoPicker picker;
  late ServiceRequestProvider requests;
  late PhotoAuth auth;
  setUp(() {
    api = PhotoApi();
    picker = FakePhotoPicker();
    auth = PhotoAuth();
    requests = ServiceRequestProvider(serviceRequestService: api);
  });
  tearDown(() {
    requests.dispose();
    auth.dispose();
  });
  Widget app(Widget home, {double scale = 1}) => MultiProvider(
    providers: [
      ChangeNotifierProvider<ServiceRequestProvider>.value(value: requests),
      ChangeNotifierProvider<AuthProvider>.value(value: auth),
    ],
    child: MaterialApp(
      theme: AppTheme.lightTheme,
      builder: (context, child) => MediaQuery(
        data: MediaQuery.of(context)
            .copyWith(textScaler: TextScaler.linear(scale)),
        child: child!,
      ),
      home: home,
    ),
  );
  Future<void> tap(WidgetTester tester, String text) async {
    await tester.ensureVisible(find.text(text).last);
    await tester.tap(find.text(text).last);
    await tester.pumpAndSettle();
  }

  Future<void> select(WidgetTester tester, int i, {bool camera = false}) async {
    picker.next = photo(
      i,
      source: camera ? PhotoSource.camera : PhotoSource.gallery,
    );
    await tap(tester, 'Add Photo');
    await tap(tester, camera ? 'Take Photo' : 'Choose from Gallery');
  }

  Future<void> review(WidgetTester tester) async {
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
  }

  for (final camera in [true, false]) {
    testWidgets(
      '${camera ? 'camera' : 'gallery'} choice adds optional thumbnail through fake picker',
      (tester) async {
        await tester.pumpWidget(
          app(CreateServiceRequestScreen(imagePicker: picker)),
        );
        await tester.pumpAndSettle();
        expect(find.text('Problem Photos (Optional)'), findsOneWidget);
        await select(tester, 1, camera: camera);
        expect(picker.calls, [
          camera ? PhotoSource.camera : PhotoSource.gallery,
        ]);
        expect(find.byType(PrivatePhotoImage), findsOneWidget);
        expect(tester.takeException(), isNull);
      },
    );
  }
  testWidgets('source sheet cancellation does not show an error', (
    tester,
  ) async {
    await tester.pumpWidget(
      app(CreateServiceRequestScreen(imagePicker: picker)),
    );
    await tester.pumpAndSettle();
    await tap(tester, 'Add Photo');
    expect(find.text('Take Photo'), findsOneWidget);
    expect(find.text('Choose from Gallery'), findsOneWidget);
    await tap(tester, 'Choose from Gallery');
    expect(find.byType(PrivatePhotoImage), findsNothing);
    expect(find.textContaining('Could not'), findsNothing);
  });
  testWidgets(
    'three photos disable Add and selected removal does not call backend',
    (tester) async {
      await tester.pumpWidget(
        app(CreateServiceRequestScreen(imagePicker: picker)),
      );
      await tester.pumpAndSettle();
      for (var i = 1; i <= 3; i++) {
        await select(tester, i);
      }
      expect(find.text('Maximum 3 photos'), findsOneWidget);
      expect(
        tester
            .widget<OutlinedButton>(
              find.widgetWithText(OutlinedButton, 'Add Photo'),
            )
            .onPressed,
        isNull,
      );
      await tester.ensureVisible(find.byTooltip('Remove selected photo 2'));
      await tester.tap(find.byTooltip('Remove selected photo 2'));
      await tester.pumpAndSettle();
      expect(find.byType(PrivatePhotoImage), findsNWidgets(2));
      expect(api.events, isEmpty);
    },
  );
  testWidgets(
    'photos survive wizard forward and back navigation and appear in Review',
    (tester) async {
      await tester.pumpWidget(
        app(CreateServiceRequestScreen(imagePicker: picker)),
      );
      await tester.pumpAndSettle();
      await select(tester, 1);
      await review(tester);
      expect(find.text('1 selected'), findsOneWidget);
      expect(find.byType(PrivatePhotoImage), findsOneWidget);
      await tap(tester, 'Back');
      await tap(tester, 'Back');
      expect(find.byType(PrivatePhotoImage), findsOneWidget);
      expect(find.text('Water is leaking under the sink'), findsOneWidget);
      expect(api.events, isEmpty);
    },
  );
  testWidgets(
    'partial failure retries only attachment and then opens existing request',
    (tester) async {
      api.failures.add('photo2.png');
      await tester.pumpWidget(
        app(CreateServiceRequestScreen(imagePicker: picker)),
      );
      await tester.pumpAndSettle();
      await select(tester, 1);
      await select(tester, 2);
      await review(tester);
      await tap(tester, 'Submit Request');
      expect(find.text('1 of 2 photos uploaded'), findsOneWidget);
      expect(find.text('Continue without failed photos'), findsOneWidget);
      api.failures.clear();
      await tap(tester, 'Retry failed photos');
      expect(find.text('Request Details'), findsOneWidget);
      expect(api.events.where((e) => e == 'create').length, 1);
      expect(api.events.where((e) => e.endsWith('photo1.png')).length, 1);
      expect(api.events.where((e) => e.endsWith('photo2.png')).length, 2);
    },
  );
  testWidgets(
    'continue without failed optional photo retains created request',
    (tester) async {
      api.failures.add('photo1.png');
      await tester.pumpWidget(
        app(CreateServiceRequestScreen(imagePicker: picker)),
      );
      await tester.pumpAndSettle();
      await select(tester, 1);
      await review(tester);
      await tap(tester, 'Submit Request');
      await tap(tester, 'Continue without failed photos');
      expect(find.text('Request Details'), findsOneWidget);
      expect(api.events.where((e) => e == 'create').length, 1);
      expect(api.events.where((e) => e.startsWith('delete:')), isEmpty);
    },
  );
  for (final status in ServiceRequestStatus.values) {
    testWidgets('$status attachment visibility and lifecycle remove control', (
      tester,
    ) async {
      api.status = status;
      api.rows.add(attachment(1));
      requests.setCurrentRequest(request(status: status));
      await tester.pumpWidget(
        app(
          ServiceRequestDetailScreen(
            requestId: 'request-1',
            imagePicker: picker,
          ),
        ),
      );
      if (status == ServiceRequestStatus.analyzing) {
        await tester.pump();
        await tester.pump(const Duration(milliseconds: 100));
        await tester.pump();
      } else {
        await tester.pumpAndSettle();
      }
      expect(find.byType(PrivatePhotoImage), findsOneWidget);
      final editable =
          status == ServiceRequestStatus.created ||
          status == ServiceRequestStatus.awaitingInformation;
      expect(
        find.byTooltip('Remove uploaded problem photo 1'),
        editable ? findsOneWidget : findsNothing,
      );
      expect(api.events, contains('content:attachment-1'));
      expect(tester.takeException(), isNull);
    });
  }
  testWidgets(
    'thumbnail opens authorized preview; removal confirms and refreshes',
    (tester) async {
      api.rows.add(attachment(1));
      requests.setCurrentRequest(request());
      await tester.pumpWidget(
        app(
          ServiceRequestDetailScreen(
            requestId: 'request-1',
            imagePicker: picker,
          ),
        ),
      );
      await tester.pumpAndSettle();
      final thumbnail = find.byType(PrivatePhotoImage);
      await tester.ensureVisible(thumbnail);
      await tester.tap(thumbnail);
      await tester.pumpAndSettle();
      expect(find.byType(Dialog), findsOneWidget);
      expect(
        find.byTooltip('Remove uploaded problem photo 1').hitTestable(),
        findsNothing,
      );
      await tester.tap(find.byTooltip('Close photo'));
      await tester.pumpAndSettle();
      await tester.ensureVisible(
        find.byTooltip('Remove uploaded problem photo 1'),
      );
      await tester.tap(find.byTooltip('Remove uploaded problem photo 1'));
      await tester.pumpAndSettle();
      expect(api.events.where((e) => e.startsWith('delete:')), isEmpty);
      await tap(tester, 'Remove Photo');
      expect(api.rows, isEmpty);
      expect(find.text('No photos added'), findsOneWidget);
      expect(api.events.last, 'detail');
    },
  );
  testWidgets(
    'list error leaves description and workflow usable and supports retry',
    (tester) async {
      api.failList = true;
      requests.setCurrentRequest(request());
      await tester.pumpWidget(
        app(
          ServiceRequestDetailScreen(
            requestId: 'request-1',
            imagePicker: picker,
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.text(request().description), findsOneWidget);
      expect(find.text('Analyze with AssistLK AI'), findsOneWidget);
      expect(find.text('Retry photos'), findsOneWidget);
      api.failList = false;
      await tap(tester, 'Retry photos');
      expect(find.text('No photos added'), findsOneWidget);
    },
  );
  testWidgets(
    'AwaitingInformation photo is explicitly uploaded and never triggers analysis',
    (tester) async {
      api.status = ServiceRequestStatus.awaitingInformation;
      requests.setCurrentRequest(request(status: api.status));
      await tester.pumpWidget(
        app(
          ServiceRequestDetailScreen(
            requestId: 'request-1',
            imagePicker: picker,
          ),
        ),
      );
      await tester.pumpAndSettle();
      await select(tester, 1);
      expect(api.rows, isEmpty);
      expect(
        find.text(
          'Upload or discard selected photos before continuing with analysis.',
        ),
        findsOneWidget,
      );
      await tap(tester, 'Upload Photos');
      expect(api.rows.length, 1);
      expect(api.status, ServiceRequestStatus.awaitingInformation);
      expect(api.events.where((e) => e == 'create'), isEmpty);
    },
  );
  for (final width in [320.0, 412.0]) {
    testWidgets(
      'complete photo wizard and detail at width $width enlarged text',
      (tester) async {
        tester.view.physicalSize = Size(width, 1000);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        await tester.pumpWidget(
          app(CreateServiceRequestScreen(imagePicker: picker), scale: 1.8),
        );
        await tester.pumpAndSettle();
        expect(tester.takeException(), isNull);
        await select(tester, 1);
        await review(tester);
        expect(tester.takeException(), isNull);
        await tap(tester, 'Submit Request');
        expect(find.text('Request Details'), findsOneWidget);
        expect(tester.takeException(), isNull);
      },
    );
    for (final scale in [1.0, 1.8]) {
      testWidgets(
        'photo selection and errors fit width $width text scale $scale',
        (tester) async {
          tester.view.physicalSize = Size(width, 900);
          tester.view.devicePixelRatio = 1;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);
          final controller = ProblemPhotosController(
            service: api,
            picker: picker,
            auth: auth,
          );
          for (var i = 1; i <= 3; i++) {
            picker.next = photo(i);
            await controller.pick(PhotoSource.gallery);
          }
          await tester.pumpWidget(
            app(
              Scaffold(
                body: SingleChildScrollView(
                  child: DraftProblemPhotos(controller: controller),
                ),
              ),
              scale: scale,
            ),
          );
          await tester.pumpAndSettle();
          expect(find.byType(PrivatePhotoImage), findsNWidgets(3));
          expect(tester.takeException(), isNull);
          api.failures.add('photo2.png');
          await controller.submit(create: () async => request());
          await tester.pumpWidget(
            app(
              Scaffold(
                body: SingleChildScrollView(
                  child: PhotoUploadProgress(
                    controller: controller,
                    onRetry: () {},
                    onContinue: () {},
                  ),
                ),
              ),
              scale: scale,
            ),
          );
          await tester.pumpAndSettle();
          expect(find.text('2 of 3 photos uploaded'), findsOneWidget);
          expect(tester.takeException(), isNull);
          await tester.pumpWidget(const SizedBox());
          controller.dispose();
        },
      );
    }
  }
}
