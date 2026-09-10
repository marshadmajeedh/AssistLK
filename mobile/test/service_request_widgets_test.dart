import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/service_requests/models/problem_understanding_result_model.dart';
import 'package:mobile/features/service_requests/models/service_request_clarification_model.dart';
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
    testWidgets('legacy fallback renders follow-up questions and responds to button taps', (tester) async {
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

    testWidgets('renders pending questions with text inputs and validates non-empty submission', (tester) async {
      int? submittedRound;
      Map<String, String>? submittedAnswers;

      final clarifications = [
        const ServiceRequestClarificationModel(
          id: 'q-1',
          clarificationRound: 1,
          sequence: 1,
          question: 'Is the leak under the sink or at the spout?',
        ),
        const ServiceRequestClarificationModel(
          id: 'q-2',
          clarificationRound: 1,
          sequence: 2,
          question: 'What type of pipe material is visible?',
        ),
      ];

      await tester.pumpWidget(
        buildTestable(
          ClarificationSection(
            clarifications: clarifications,
            onEditDetails: () {},
            onReanalyze: () {},
            onSubmitAnswers: (round, answers) async {
              submittedRound = round;
              submittedAnswers = answers;
            },
          ),
        ),
      );

      expect(find.text('Round 1 of 2'), findsOneWidget);
      expect(find.text('Is the leak under the sink or at the spout?'), findsOneWidget);
      expect(find.text('What type of pipe material is visible?'), findsOneWidget);
      expect(find.byType(TextFormField), findsNWidgets(2));

      // Attempt to submit empty form
      await tester.tap(find.text('Submit & Re-analyze'));
      await tester.pumpAndSettle();

      expect(find.text('Please provide an answer to this question.'), findsNWidgets(2));
      expect(submittedAnswers, isNull);

      // Enter answers
      await tester.enterText(find.byType(TextFormField).at(0), 'Under the kitchen sink');
      await tester.enterText(find.byType(TextFormField).at(1), 'PVC plastic pipe');
      await tester.tap(find.text('Submit & Re-analyze'));
      await tester.pumpAndSettle();

      expect(submittedRound, 1);
      expect(submittedAnswers, {
        'q-1': 'Under the kitchen sink',
        'q-2': 'PVC plastic pipe',
      });
    });

    testWidgets('renders fully answered state with submitted answers and Re-analyze button', (tester) async {
      bool reanalyzeTapped = false;

      final clarifications = [
        ServiceRequestClarificationModel(
          id: 'q-1',
          clarificationRound: 1,
          sequence: 1,
          question: 'Is the leak under the sink?',
          answer: 'Yes, directly beneath the P-trap',
          answeredAt: DateTime.now(),
        ),
      ];

      await tester.pumpWidget(
        buildTestable(
          ClarificationSection(
            clarifications: clarifications,
            onEditDetails: () {},
            onReanalyze: () => reanalyzeTapped = true,
          ),
        ),
      );

      // No text fields should be rendered for answered questions
      expect(find.byType(TextFormField), findsNothing);
      expect(find.text('Is the leak under the sink?'), findsOneWidget);
      expect(find.text('Your Answer: Yes, directly beneath the P-trap'), findsOneWidget);
      expect(find.text('Answers for Round 1 have been submitted. Tap Re-analyze to continue with provider matching.'), findsOneWidget);

      await tester.tap(find.text('Re-analyze'));
      expect(reanalyzeTapped, true);
    });

    testWidgets('renders max rounds reached card without empty input fields or fake fallback questions', (tester) async {
      bool editTapped = false;

      final clarifications = [
        ServiceRequestClarificationModel(
          id: 'q-1',
          clarificationRound: 2,
          sequence: 1,
          question: 'Round 2 question',
          answer: 'Round 2 answer',
          answeredAt: DateTime.now(),
        ),
      ];

      await tester.pumpWidget(
        buildTestable(
          ClarificationSection(
            clarifications: clarifications,
            hasReachedMaxRounds: true,
            onEditDetails: () => editTapped = true,
            onReanalyze: () {},
          ),
        ),
      );

      expect(find.text('Further Details Needed'), findsOneWidget);
      expect(find.text('Maximum clarification rounds (2 of 2) have been completed. Please edit your request description with more specific details so our AI can accurately classify your request.'), findsOneWidget);
      expect(find.text('Edit Details'), findsOneWidget);
      expect(find.text('Please provide further details regarding the issue.'), findsNothing);
      expect(find.byType(TextFormField), findsNothing);

      await tester.tap(find.text('Edit Details'));
      expect(editTapped, true);
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
