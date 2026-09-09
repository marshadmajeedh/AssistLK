import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/service_requests/models/problem_understanding_result_model.dart';
import 'package:mobile/features/service_requests/models/service_request_model.dart';
import 'package:mobile/features/service_requests/models/service_request_status.dart';
import 'package:mobile/features/service_requests/models/service_request_urgency.dart';
import 'package:mobile/features/service_requests/widgets/analysis_result_card.dart';
import 'package:mobile/features/service_requests/widgets/clarification_section.dart';
import 'package:mobile/features/service_requests/widgets/ready_for_matching_section.dart';
import 'package:mobile/features/service_requests/widgets/service_request_card.dart';
import 'package:mobile/features/service_requests/widgets/status_badge.dart';
import 'package:mobile/features/service_requests/widgets/urgency_chip.dart';
import 'package:mobile/shared/theme/app_theme.dart';

void main() {
  Widget buildTestable(Widget child) {
    return MaterialApp(
      theme: AppTheme.lightTheme,
      home: Scaffold(
        body: SingleChildScrollView(child: child),
      ),
    );
  }

  group('StatusBadge', () {
    testWidgets('renders all status labels correctly', (tester) async {
      for (final status in ServiceRequestStatus.values) {
        await tester.pumpWidget(buildTestable(StatusBadge(status: status)));
        expect(find.text(status.displayName), findsOneWidget);
      }
    });
  });

  group('UrgencyChip', () {
    testWidgets('renders all urgency labels correctly', (tester) async {
      for (final urgency in ServiceRequestUrgency.values) {
        await tester.pumpWidget(buildTestable(UrgencyChip(urgency: urgency)));
        expect(find.text(urgency.displayName), findsOneWidget);
      }
    });
  });

  group('ServiceRequestCard', () {
    testWidgets('renders request details, status badge, and urgency chip', (tester) async {
      final request = ServiceRequestModel(
        serviceRequestId: 'req-1',
        customerId: 'cust-1',
        category: 'Plumbing',
        description: 'Water leaking from kitchen faucet',
        locationText: 'Colombo 04',
        urgency: ServiceRequestUrgency.medium,
        status: ServiceRequestStatus.analyzing,
        createdAt: DateTime(2026, 9, 9),
        updatedAt: DateTime(2026, 9, 9),
      );

      bool tapped = false;

      await tester.pumpWidget(
        buildTestable(
          ServiceRequestCard(
            request: request,
            onTap: () => tapped = true,
          ),
        ),
      );

      expect(find.text('Plumbing'), findsOneWidget);
      expect(find.text('Water leaking from kitchen faucet'), findsOneWidget);
      expect(find.text('Analyzing'), findsOneWidget);
      expect(find.text('Medium'), findsOneWidget);
      expect(find.text('2026-09-09'), findsOneWidget);

      await tester.tap(find.byType(ServiceRequestCard));
      expect(tapped, true);
    });
  });

  group('AnalysisResultCard', () {
    testWidgets('renders AI analysis summary, category, urgency, and confidence', (tester) async {
      const analysis = ProblemUnderstandingResultModel(
        workflowId: 'wf-1',
        executionId: 'ex-1',
        serviceRequestId: 'req-1',
        status: ServiceRequestStatus.analyzed,
        category: 'Electrical Wiring',
        problemSummary: 'Short circuit detected in main distribution box',
        urgency: ServiceRequestUrgency.critical,
        confidence: 0.95,
        needsMoreInformation: false,
        followUpQuestions: [],
      );

      await tester.pumpWidget(buildTestable(const AnalysisResultCard(analysis: analysis)));

      expect(find.text('AI Analysis Result'), findsOneWidget);
      expect(find.text('Electrical Wiring'), findsOneWidget);
      expect(find.text('Critical'), findsOneWidget);
      expect(find.text('95% Confidence'), findsOneWidget);
      expect(find.text('Short circuit detected in main distribution box'), findsOneWidget);
    });
  });

  group('ClarificationSection', () {
    testWidgets('renders follow-up questions and responds to button taps', (tester) async {
      bool editTapped = false;
      bool reanalyzeTapped = false;

      await tester.pumpWidget(
        buildTestable(
          ClarificationSection(
            followUpQuestions: const [
              'Are other electrical appliances affected?',
              'Does the breaker trip immediately or after a delay?',
            ],
            onEditDetails: () => editTapped = true,
            onReanalyze: () => reanalyzeTapped = true,
          ),
        ),
      );

      expect(find.text('Clarification Needed'), findsOneWidget);
      expect(find.text('Are other electrical appliances affected?'), findsOneWidget);
      expect(find.text('Does the breaker trip immediately or after a delay?'), findsOneWidget);

      await tester.tap(find.text('Edit Details'));
      expect(editTapped, true);

      await tester.tap(find.text('Re-analyze'));
      expect(reanalyzeTapped, true);
    });
  });

  group('ReadyForMatchingSection', () {
    testWidgets('renders confirmation messages and proceed button', (tester) async {
      bool proceedTapped = false;

      await tester.pumpWidget(
        buildTestable(
          ReadyForMatchingSection(
            onProceedToMatching: () => proceedTapped = true,
          ),
        ),
      );

      expect(find.text('Request Understood'), findsOneWidget);
      expect(find.text('Ready for provider matching'), findsOneWidget);

      await tester.tap(find.text('Find Matching Providers'));
      expect(proceedTapped, true);
    });
  });
}
