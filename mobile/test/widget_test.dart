import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/tracking/customer_job_tracking_screen.dart';
import 'package:mobile/features/tracking/models/tracking_coordinate.dart';
import 'package:mobile/features/tracking/provider_job_tracking_screen.dart';
import 'package:mobile/features/providers/widgets/provider_bottom_navigation.dart';
import 'package:mobile/core/api/api_client.dart';
import 'package:mobile/features/providers/providers/provider_dashboard_provider.dart';
import 'package:mobile/features/providers/services/provider_service.dart';

void main() {
  test('AssistLK basic test', () {
    expect('AssistLK', 'AssistLK');
  });

  test('tracking coordinates accept SignalR JSON casing', () {
    final coordinate = TrackingCoordinate.fromSignalR({
      'JobId': 'job-1',
      'Latitude': 6.91,
      'Longitude': 79.87,
      'UpdatedAtUtc': '2026-10-04T10:00:00Z',
    });

    expect(coordinate.jobId, 'job-1');
    expect(coordinate.latitude, 6.91);
    expect(coordinate.longitude, 79.87);
    expect(coordinate.updatedAtUtc, DateTime.utc(2026, 10, 4, 10));
  });

  test('provider completion updates dashboard stats once and notifies listeners', () {
    final dashboard = ProviderDashboardProvider(
      providerService: ProviderService(apiClient: ApiClient()),
    )
      ..profile = {'completedJobs': 2, 'totalCompletedJobs': 2}
      ..activeJobMatch = {'jobId': 'job-1', 'status': 'Accepted'};
    var notifications = 0;
    dashboard.addListener(() => notifications++);

    dashboard.markJobCompletedLocally('job-1');
    dashboard.markJobCompletedLocally('job-1');

    expect(dashboard.profile?['completedJobs'], 3);
    expect(dashboard.profile?['totalCompletedJobs'], 3);
    expect(dashboard.activeJobMatch?['status'], 'Completed');
    expect(notifications, 1);

    dashboard.dispose();
  });

  testWidgets('provider navigation places Tracking after Bookings and selects it', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(420, 900);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    var selectedIndex = 0;
    await tester.pumpWidget(
      MaterialApp(
        home: StatefulBuilder(
          builder: (context, setState) => Scaffold(
            bottomNavigationBar: ProviderBottomNavigation(
              selectedIndex: selectedIndex,
              onDestinationSelected: (index) =>
                  setState(() => selectedIndex = index),
            ),
          ),
        ),
      ),
    );

    expect(ProviderBottomNavigation.labels, [
      'Dashboard',
      'Dispatch Map',
      'Bookings',
      'Tracking',
      'Profile',
    ]);
    await tester.tap(find.byKey(const ValueKey('provider_navigation_Tracking')));
    await tester.pumpAndSettle();

    expect(selectedIndex, 3);
  });

  testWidgets('completed tracking screen preserves statuses and proof/review UI', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(320, 480);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      const MaterialApp(
        home: CustomerJobTrackingScreen(
          jobId: 'job-1',
          status: 'Job Completed Successfully',
          destinationLatitude: 0,
          destinationLongitude: 0,
          additionalCost: 1000,
          feedbackComment: 'The provider fixed the wiring quickly.',
        ),
      ),
    );
    await tester.pump(const Duration(milliseconds: 100));

    expect(find.text('How was your experience?'), findsOneWidget);
    expect(find.text('Assigned'), findsOneWidget);
    expect(find.text('OnTheWay'), findsOneWidget);
    expect(find.text('Arrived'), findsOneWidget);
    expect(find.text('InProgress'), findsOneWidget);
    expect(find.text('Completed'), findsOneWidget);
    expect(find.text('Service Proof Image'), findsOneWidget);
    expect(find.text('Proof photo'), findsOneWidget);
    expect(find.text('Submit Review'), findsOneWidget);
    expect(find.text('1000'), findsNothing);
    expect(
      tester.widget<TextField>(find.byType(TextField)).controller!.text,
      'The provider fixed the wiring quickly.',
    );
    expect(
      find.ancestor(
        of: find.text('How was your experience?'),
        matching: find.byType(SingleChildScrollView),
      ),
      findsOneWidget,
    );

    final completedIcon = tester.widget<CircleAvatar>(
      find.byKey(const ValueKey('completion_status_icon')),
    );
    expect(completedIcon.backgroundColor, const Color(0xFF1F4E78));

    final completedStage = tester.widget<Container>(
      find.byKey(const ValueKey('customer_tracking_stage_Completed')),
    );
    expect(
      (completedStage.decoration! as BoxDecoration).color,
      const Color(0xFF1F4E78),
    );
    expect(tester.takeException(), isNull);
  });

  testWidgets('provider tracking shows visual progress and available details', (
    tester,
  ) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: ProviderJobTrackingScreen(
          jobId: 'job-2',
          initialStatus: 'InProgress',
          customerAddress: '42 Lake Road',
          serviceDescription: 'Kitchen sink repair',
        ),
      ),
    );

    expect(find.text('Service Progress'), findsOneWidget);
    expect(find.text('Assigned'), findsOneWidget);
    expect(find.text('On The Way'), findsOneWidget);
    expect(find.text('Arrived'), findsOneWidget);
    expect(find.text('In Progress'), findsOneWidget);
    expect(find.text('Completed'), findsOneWidget);
    expect(find.text('You are currently working on this'), findsOneWidget);
    expect(find.text('42 Lake Road'), findsOneWidget);
    expect(find.text('Kitchen sink repair'), findsOneWidget);
    expect(find.text('Mark as Completed'), findsOneWidget);

    final completedIndicator = tester.widget<Container>(
      find.byKey(const ValueKey('tracking_step_Arrived_indicator')),
    );
    expect(
      (completedIndicator.decoration! as BoxDecoration).color,
      const Color(0xFF2E7D32),
    );
    final activeContent = tester.widget<AnimatedContainer>(
      find.byKey(const ValueKey('tracking_step_InProgress_content')),
    );
    expect(
      (activeContent.decoration! as BoxDecoration).color,
      const Color(0xFFEFF6FF),
    );
    expect(tester.takeException(), isNull);
  });
}
