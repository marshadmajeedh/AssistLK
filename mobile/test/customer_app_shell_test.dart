import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';
import 'package:mobile/app/app.dart';
import 'package:mobile/app/customer_app_shell.dart';
import 'package:mobile/core/api/api_client.dart';
import 'package:mobile/features/auth/models/auth_user.dart';
import 'package:mobile/features/auth/providers/auth_provider.dart';
import 'package:mobile/features/auth/screens/login_screen.dart';
import 'package:mobile/features/providers/provider_home_screen.dart';
import 'package:mobile/features/service_requests/models/create_service_request_dto.dart';
import 'package:mobile/features/service_requests/models/service_request_model.dart';
import 'package:mobile/features/service_requests/models/service_request_status.dart';
import 'package:mobile/features/service_requests/screens/create_service_request_screen.dart';
import 'package:mobile/features/service_requests/screens/customer_activity_screen.dart';
import 'package:mobile/features/service_requests/screens/customer_services_screen.dart';
import 'package:mobile/features/service_requests/screens/service_request_detail_screen.dart';
import 'package:mobile/features/service_requests/screens/edit_service_request_screen.dart';
import 'package:mobile/features/service_requests/widgets/service_category_card.dart';
import 'package:mobile/features/service_requests/widgets/service_request_card.dart';
import 'package:mobile/features/service_requests/widgets/status_badge.dart';

import 'session_foundation_test.dart'
    show MemoryStorage, AuthStub, RequestsStub, request;

class ShellRequests extends RequestsStub {
  ShellRequests(super.apiClient);
  int loads = 0;
  bool fail = false;
  CreateServiceRequestDto? submitted;
  @override
  Future<ServiceRequestModel> getById(String id) async => items.firstWhere(
    (item) => item.serviceRequestId == id,
    orElse: () => request(id),
  );
  @override
  Future<List<ServiceRequestModel>> getMyRequests() {
    loads++;
    if (fail) return Future.error(Exception('offline'));
    return super.getMyRequests();
  }

  @override
  Future<ServiceRequestModel> create(CreateServiceRequestDto dto) async {
    submitted = dto;
    return request('new');
  }
}

void main() {
  late AuthProvider auth;
  late AuthStub authService;
  late ShellRequests requests;
  setUp(() {
    final storage = MemoryStorage();
    final api = ApiClient(tokenStorage: storage);
    authService = AuthStub(api);
    auth = AuthProvider(authService: authService, tokenStorage: storage);
    requests = ShellRequests(api);
  });
  Future<void> mount(WidgetTester tester) async {
    await auth.login(email: 'a@example.com', password: 'password');
    await tester.pumpWidget(
      ChangeNotifierProvider<AuthProvider>.value(
        value: auth,
        child: AssistLKApp(serviceRequestService: requests),
      ),
    );
    await tester.pump();
  }

  Finder destination(String label) =>
      find.widgetWithText(NavigationDestination, label);
  Future<void> tab(WidgetTester tester, String label) async {
    await tester.tap(destination(label));
    await tester.pumpAndSettle();
  }

  int selected(WidgetTester tester) =>
      tester.widget<NavigationBar>(find.byType(NavigationBar)).selectedIndex;
  Future<void> tapText(WidgetTester tester, String label) async {
    final finder = find.text(label).first;
    await tester.ensureVisible(finder);
    await tester.tap(finder);
    await tester.pumpAndSettle();
  }

  testWidgets(
    'Customer has four destinations, Home initially, one list fetch',
    (tester) async {
      await mount(tester);
      await tester.pumpAndSettle();
      expect(find.byType(CustomerAppShell), findsOneWidget);
      final nav = tester.widget<NavigationBar>(find.byType(NavigationBar));
      expect(
        nav.destinations.cast<NavigationDestination>().map((d) => d.label),
        ['Home', 'Services', 'Activity', 'Account'],
      );
      expect(selected(tester), 0);
      for (final label in ['Services', 'Activity', 'Account', 'Home']) {
        await tab(tester, label);
      }
      expect(requests.loads, 1);
      expect(find.byType(Navigator), findsOneWidget);
    },
  );

  for (final entry in {'Services': 1, 'Activity': 2, 'Account': 3}.entries) {
    testWidgets(
      '${entry.key} selects correctly and Android Back returns Home',
      (tester) async {
        await mount(tester);
        await tester.pumpAndSettle();
        await tab(tester, entry.key);
        expect(selected(tester), entry.value);
        await tester.binding.handlePopRoute();
        await tester.pumpAndSettle();
        expect(selected(tester), 0);
        expect(find.byType(CustomerAppShell), findsOneWidget);
      },
    );
  }

  testWidgets('Home Back delegates to platform', (tester) async {
    int exits = 0;
    tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(
      SystemChannels.platform,
      (call) async {
        if (call.method == 'SystemNavigator.pop') exits++;
        return null;
      },
    );
    addTearDown(
      () => tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(
        SystemChannels.platform,
        null,
      ),
    );
    await mount(tester);
    await tester.pumpAndSettle();
    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();
    expect(exits, 1);
  });

  testWidgets('Home Create is full screen and Back restores Home', (
    tester,
  ) async {
    await mount(tester);
    await tester.pumpAndSettle();
    await tapText(tester, 'Create Service Request');
    expect(find.byType(CreateServiceRequestScreen), findsOneWidget);
    expect(find.byType(NavigationBar), findsNothing);
    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();
    expect(selected(tester), 0);
    expect(requests.loads, 1);
  });

  for (final entry in <String, String?>{
    'Plumbing': 'Plumbing',
    'Electrical': 'Electrical',
    'Vehicle Assistance': 'Vehicle Repair',
    'Appliance Repair': 'Appliance Repair',
    'Not sure what service you need?': null,
  }.entries) {
    testWidgets('${entry.key} submits canonical hint and returns to Services', (
      tester,
    ) async {
      await mount(tester);
      await tester.pumpAndSettle();
      await tab(tester, 'Services');
      await tapText(tester, entry.key);
      expect(find.byType(NavigationBar), findsNothing);
      expect(
        tester
            .widget<CreateServiceRequestScreen>(
              find.byType(CreateServiceRequestScreen),
            )
            .initialCategoryPreference,
        entry.value,
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Problem Description'),
        'A leaking pipe needs attention in the kitchen',
      );
      await tapText(tester, 'Next: Location');
      await tester.enterText(
        find.widgetWithText(TextFormField, 'Location / Address'),
        'Colombo 03',
      );
      await tapText(tester, 'Next: Review');
      await tapText(tester, 'Submit Request');
      expect(requests.submitted!.categoryHint, entry.value);
      expect(find.byType(ServiceRequestDetailScreen), findsOneWidget);
      expect(find.byType(NavigationBar), findsNothing);
      await tester.binding.handlePopRoute();
      await tester.pumpAndSettle();
      expect(selected(tester), 1);
      expect(requests.loads, 1);
    });
  }

  testWidgets('Services Create Back restores scroll and selected tab', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(320, 640);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await mount(tester);
    await tester.pumpAndSettle();
    await tab(tester, 'Services');
    final card = find.widgetWithText(ServiceCategoryCard, 'Appliance Repair');
    await tester.ensureVisible(card);
    await tester.pumpAndSettle();
    final scroll = tester.state<ScrollableState>(
      find
          .descendant(
            of: find.byType(CustomerServicesScreen),
            matching: find.byType(Scrollable),
          )
          .first,
    );
    final offset = scroll.position.pixels;
    expect(offset, greaterThan(0));
    await tab(tester, 'Account');
    await tab(tester, 'Services');
    expect(scroll.position.pixels, offset);
    await tester.tap(card);
    await tester.pumpAndSettle();
    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();
    expect(selected(tester), 1);
    expect(scroll.position.pixels, offset);
  });

  testWidgets(
    'Activity detail and edit push above shell and return to Activity',
    (tester) async {
      requests.items = [
        request('A').copyWith(status: ServiceRequestStatus.awaitingInformation),
      ];
      await mount(tester);
      await tester.pumpAndSettle();
      await tab(tester, 'Activity');
      await tester.tap(find.byType(ServiceRequestCard));
      await tester.pumpAndSettle();
      expect(find.byType(ServiceRequestDetailScreen), findsOneWidget);
      expect(find.byType(NavigationBar), findsNothing);
      await tapText(tester, 'Edit Details');
      expect(find.byType(EditServiceRequestScreen), findsOneWidget);
      expect(find.byType(NavigationBar), findsNothing);
      await tester.binding.handlePopRoute();
      await tester.pumpAndSettle();
      expect(find.byType(ServiceRequestDetailScreen), findsOneWidget);
      await tester.binding.handlePopRoute();
      await tester.pumpAndSettle();
      expect(selected(tester), 2);
    },
  );

  testWidgets(
    'Activity renders every existing lifecycle status from shared data',
    (tester) async {
      requests.items = [
        for (final status in ServiceRequestStatus.values)
          request(status.name).copyWith(status: status),
      ];
      await mount(tester);
      await tester.pumpAndSettle();
      await tab(tester, 'Activity');
      for (final status in ServiceRequestStatus.values) {
        final badge = find.byWidgetPredicate(
          (widget) => widget is StatusBadge && widget.status == status,
        );
        await tester.scrollUntilVisible(
          badge,
          160,
          scrollable: find.descendant(
            of: find.byType(CustomerActivityScreen),
            matching: find.byType(Scrollable),
          ),
        );
        expect(badge, findsOneWidget);
      }
      expect(requests.loads, 1);
    },
  );

  testWidgets('Activity empty state', (tester) async {
    requests.items = [];
    await mount(tester);
    await tester.pumpAndSettle();
    await tab(tester, 'Activity');
    expect(find.text('No Service Requests Yet'), findsOneWidget);
    expect(find.byType(ServiceRequestCard), findsNothing);
  });

  testWidgets('Activity pending initial load does not trigger another fetch', (
    tester,
  ) async {
    requests.pending = Completer<List<ServiceRequestModel>>();
    await mount(tester);
    await tester.tap(destination('Activity'));
    await tester.pump();
    expect(find.byType(CircularProgressIndicator), findsOneWidget);
    expect(find.text('No Service Requests Yet'), findsNothing);
    expect(requests.loads, 1);
    requests.pending!.complete([request('A')]);
    await tester.pumpAndSettle();
    expect(find.byType(ServiceRequestCard), findsOneWidget);
  });

  testWidgets('Activity failure offers Retry using the shared provider', (
    tester,
  ) async {
    requests.fail = true;
    await mount(tester);
    await tester.pumpAndSettle();
    await tab(tester, 'Activity');
    expect(find.text('Retry'), findsOneWidget);
    expect(find.text('No Service Requests Yet'), findsNothing);
    requests.fail = false;
    await tapText(tester, 'Retry');
    expect(find.byType(ServiceRequestCard), findsOneWidget);
    expect(requests.loads, 2);
  });

  testWidgets(
    'Activity pull refresh updates list and preserves scroll across tabs',
    (tester) async {
      requests.items = List.generate(15, (i) => request('$i'));
      await mount(tester);
      await tester.pumpAndSettle();
      await tab(tester, 'Activity');
      final scrollable = find.descendant(
        of: find.byType(CustomerActivityScreen),
        matching: find.byType(Scrollable),
      );
      await tester.drag(scrollable, const Offset(0, -400));
      await tester.pumpAndSettle();
      final scroll = tester.state<ScrollableState>(scrollable);
      final offset = scroll.position.pixels;
      await tab(tester, 'Home');
      await tab(tester, 'Activity');
      expect(scroll.position.pixels, offset);
      scroll.position.jumpTo(0);
      await tester.pump();
      requests.items = [request('refreshed')];
      await tester.drag(scrollable, const Offset(0, 350));
      await tester.pumpAndSettle();
      expect(requests.loads, 2);
      expect(find.text('Leaking kitchen pipe refreshed'), findsOneWidget);
    },
  );

  for (final phone in [null, '0771234567']) {
    testWidgets('Account supported fields with phone $phone', (tester) async {
      authService.nextUser = AuthUser(
        userId: 'A',
        fullName: 'Customer A',
        email: 'A@example.com',
        role: 'Customer',
        phoneNumber: phone,
      );
      await mount(tester);
      await tester.pumpAndSettle();
      await tab(tester, 'Account');
      expect(find.text('Customer A'), findsOneWidget);
      expect(find.text('A@example.com'), findsOneWidget);
      expect(find.text('Role: Customer'), findsOneWidget);
      expect(find.textContaining('null'), findsNothing);
      expect(
        find.textContaining('Phone:'),
        phone == null ? findsNothing : findsOneWidget,
      );
    });
  }

  testWidgets('Account logout destroys shell; B starts Home with only B data', (
    tester,
  ) async {
    await mount(tester);
    await tester.pumpAndSettle();
    await tab(tester, 'Account');
    final oldShell = tester.state(find.byType(CustomerAppShell));
    await tapText(tester, 'Logout');
    expect(find.byType(LoginScreen), findsOneWidget);
    expect(find.byType(CustomerAppShell), findsNothing);
    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();
    expect(find.byType(CustomerAppShell), findsNothing);
    authService.nextUser = AuthStub.user('B');
    requests.items = [request('B')];
    await auth.login(email: 'B@example.com', password: 'password');
    await tester.pumpAndSettle();
    expect(selected(tester), 0);
    expect(
      identical(oldShell, tester.state(find.byType(CustomerAppShell))),
      isFalse,
    );
    await tab(tester, 'Activity');
    expect(find.text('Leaking kitchen pipe B'), findsOneWidget);
    expect(find.text('Leaking kitchen pipe A'), findsNothing);
  });

  testWidgets(
    'Provider login remains separate and does not load customer requests',
    (tester) async {
      authService.nextUser = AuthStub.user('P', 'Provider');
      await mount(tester);
      await tester.pumpAndSettle();
      expect(find.byType(ProviderHomeScreen), findsOneWidget);
      expect(find.byType(CustomerAppShell), findsNothing);
      expect(find.byType(NavigationBar), findsNothing);
      expect(requests.loads, 0);
    },
  );

  for (final size in [const Size(320, 640), const Size(640, 320)]) {
    for (final scale in [1.0, 2.0]) {
      testWidgets('all tabs usable at $size scale $scale with system insets', (
        tester,
      ) async {
        tester.view.physicalSize = size;
        tester.view.devicePixelRatio = 1;
        tester.view.padding = const FakeViewPadding(bottom: 24);
        tester.platformDispatcher.textScaleFactorTestValue = scale;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        addTearDown(tester.view.resetPadding);
        addTearDown(tester.platformDispatcher.clearTextScaleFactorTestValue);
        requests.items = [
          request('A')
              .copyWith(status: ServiceRequestStatus.awaitingInformation),
        ];
        await mount(tester);
        await tester.pumpAndSettle();
        for (final label in ['Home', 'Services', 'Activity', 'Account']) {
          await tab(tester, label);
          expect(tester.takeException(), isNull);
          expect(destination(label).hitTestable(), findsOneWidget);
          final labelFinder = find.descendant(
            of: find.byType(NavigationBar),
            matching: find.text(label),
          );
          expect(
            tester.getBottomRight(labelFinder).dy,
            lessThanOrEqualTo(size.height - 24),
          );
        }
      });
    }
  }
}
