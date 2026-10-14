import 'dart:async';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/api/api_client.dart';
import 'package:mobile/features/service_requests/models/service_request_model.dart';
import 'package:mobile/features/service_requests/providers/service_request_provider.dart';
import 'package:mobile/features/service_requests/services/service_request_service.dart';
import 'package:mobile/features/service_requests/widgets/clarification_section.dart';

Map<String, dynamic> requestJson({bool answered = false, bool analyzed = false, bool sufficient = false}) => {
  'serviceRequestId': 'request-2', 'customerId': 'customer-1',
  'category': 'Appliance Repair', 'description': 'Something is wrong with my refrigerator.',
  'locationText': 'Colombo', 'urgency': 'Medium',
  'status': sufficient ? 'Analyzed' : 'AwaitingInformation',
  'createdAt': '2026-09-12T08:00:00Z', 'updatedAt': '2026-09-12T10:00:00Z',
  'latestAnalysis': {
    'id': analyzed ? 'analysis-final' : 'analysis-before-round2',
    'detectedProblem': 'Possible refrigerator fault', 'confidence': 0.85,
    'agentName': 'ProblemUnderstandingAgent',
    'createdAt': analyzed ? '2026-09-12T11:00:00Z' : '2026-09-12T09:00:00Z',
  },
  'clarifications': [{
    'id': 'question-2', 'clarificationRound': 2, 'sequence': 1,
    'question': 'Does the refrigerator still cool?',
    'answer': answered ? 'It is not cooling and makes a continuous rattling noise.' : null,
    'answeredAt': answered ? '2026-09-12T10:00:00Z' : null,
  }],
};

/**
 * TEST SUITE: Round Two Clarification Lifecycle and Limit Enforcement Tests
 *
 * 1. What this test suite tests:
 *    - Verifies boundary conditions for the round 2 clarification limit (`hasCompletedFinalClarificationAnalysis`).
 *    - Tests that an answered round 2 request only enters the "max rounds reached / Further Details Needed" state after a persisted final analysis has been created.
 *    - Tests that reloading an answered round 2 request before analysis presents a "Re-analyze" action rather than premature limit guidance.
 *    - Tests description supersession: if a description edit supersedes questions, limit guidance requires a subsequent analysis created after the supersession timestamp.
 *    - Tests end-to-end round 2 workflow via Dio interceptors: submitting answers, triggering analysis, refreshing from backend, and displaying either "Analyzed" (sufficient) or "Further Details Needed" (insufficient), verifying that round 3 is never created or displayed.
 * 2. Why these tests are needed:
 *    - Component 1 enforces a strict 2-round clarification ceiling. Customers must never be trapped in infinite clarification loops.
 *      However, premature limit guidance must not block the user from triggering their second-round analysis.
 * 3. What is real and what is mocked:
 *    - Real `ClarificationSection` widget, `ServiceRequestModel`, and `ServiceRequestProvider`.
 *    - Mocked: Dio network client configured with `InterceptorsWrapper` to intercept `/clarifications/answers`, `/analyze`, and GET `/service-requests/{id}`.
 * 4. Prerequisites: None.
 * 5. PowerShell command to run this suite from repository root:
 *    Push-Location mobile; flutter test test/round_two_clarification_test.dart; Pop-Location
 */
void main() {
  /**
   * Purpose: What this specific test checks
   * - Verifies the boolean logic of `hasCompletedFinalClarificationAnalysis`.
   *
   * Why: Why this scenario matters
   * - Ensures limit guidance triggers only when round 2 has been both answered AND analyzed by the AI agent.
   *
   * Scenario: Input or condition being tested
   * - Test unanswered, answered without final analysis, answered with final analysis in `AwaitingInformation` status,
   *   answered with status `Analyzed` (sufficient), and answered with missing `latestAnalysis`.
   *
   * Pass condition: What result or assertion makes it pass
   * - `hasCompletedFinalClarificationAnalysis` is true ONLY when round 2 is answered and followed by a persisted analysis in `AwaitingInformation` status.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/round_two_clarification_test.dart --plain-name "answered Round 2 only reaches the limit after persisted final analysis"; Pop-Location
   */
  test('answered Round 2 only reaches the limit after persisted final analysis', () {
    expect(ServiceRequestModel.fromJson(requestJson()).hasCompletedFinalClarificationAnalysis, isFalse);
    expect(ServiceRequestModel.fromJson(requestJson(answered: true)).hasCompletedFinalClarificationAnalysis, isFalse);
    final finalJson = requestJson(answered: true, analyzed: true);
    expect(ServiceRequestModel.fromJson(finalJson).hasCompletedFinalClarificationAnalysis, isTrue);
    expect(ServiceRequestModel.fromJson(requestJson(answered: true, analyzed: true, sufficient: true)).hasCompletedFinalClarificationAnalysis, isFalse);
    finalJson['latestAnalysis'] = null;
    expect(ServiceRequestModel.fromJson(finalJson).hasCompletedFinalClarificationAnalysis, isFalse);
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that if a user reloads the app after answering round 2 (before analysis has executed), the UI offers "Re-analyze" instead of showing premature "Further Details Needed".
   *
   * Why: Why this scenario matters
   * - Prevents the customer from being stuck in a dead-end UI state if the app was closed during analysis.
   *
   * Scenario: Input or condition being tested
   * - Mount `ClarificationSection` with answered round 2 where `hasCompletedFinalClarificationAnalysis` is false.
   *
   * Pass condition: What result or assertion makes it pass
   * - 'Further Details Needed' is NOT shown; tapping 'Re-analyze' triggers the `onReanalyze` callback.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/round_two_clarification_test.dart --plain-name "answered Round 2 after reload offers Re-analyze rather than premature limit guidance"; Pop-Location
   */
  testWidgets('answered Round 2 after reload offers Re-analyze rather than premature limit guidance', (tester) async {
    var tapped = false;
    final request = ServiceRequestModel.fromJson(requestJson(answered: true));
    await tester.pumpWidget(MaterialApp(home: Scaffold(body: SingleChildScrollView(child: ClarificationSection(
      clarifications: request.clarifications,
      hasReachedMaxRounds: request.hasCompletedFinalClarificationAnalysis,
      onEditDetails: () {}, onReanalyze: () => tapped = true,
    )))));
    expect(find.text('Further Details Needed'), findsNothing);
    await tester.tap(find.text('Re-analyze'));
    expect(tapped, isTrue);
  });

  /**
   * Purpose: What this specific test checks
   * - Verifies that when customer description edits supersede questions (`supersededAt`), limit guidance is withheld until a new analysis is performed after the supersession.
   *
   * Why: Why this scenario matters
   * - Editing description provides fresh details that may resolve the problem without exhausting clarification rounds.
   *
   * Scenario: Input or condition being tested
   * - Question superseded at 12:00, prior analysis at 11:00 vs subsequent analysis at 13:00.
   *
   * Pass condition: What result or assertion makes it pass
   * - `hasCompletedFinalClarificationAnalysis` is false when analysis predates supersession, and true when analysis occurs after supersession.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/round_two_clarification_test.dart --plain-name "description supersession needs a subsequent persisted analysis before limit guidance"; Pop-Location
   */
  test('description supersession needs a subsequent persisted analysis before limit guidance', () {
    final json = requestJson(analyzed: true);
    final question = (json['clarifications'] as List).single as Map;
    question['supersededAt'] = '2026-09-12T12:00:00Z';
    expect(ServiceRequestModel.fromJson(json).hasCompletedFinalClarificationAnalysis, isFalse);
    (json['latestAnalysis'] as Map)['createdAt'] = '2026-09-12T13:00:00Z';
    expect(ServiceRequestModel.fromJson(json).hasCompletedFinalClarificationAnalysis, isTrue);
  });

  /**
   * Parameterized Round 2 E2E Workflow Tests
   * Note: Parameterized across `sufficient` flag (`[false, true]`). The test runner executes both cases.
   *
   * Purpose: What this specific test checks
   * - Verifies complete round 2 lifecycle through submission, AI analysis, status refresh, and UI presentation.
   *
   * Why: Why this scenario matters
   * - Confirms that even if the AI suggests follow-up questions for a hypothetical round 3, the client strictly suppresses round 3 and either marks the request 'Analyzed' or shows 'Further Details Needed'.
   *
   * Scenario: Input or condition being tested
   * - User enters round 2 answer, taps 'Submit & Re-analyze'. Interceptor captures POST answer, POST analyze, and GET refresh.
   *
   * Pass condition: What result or assertion makes it pass
   * - Calls match expected sequence, `provider.isAnalyzing` toggles, UI renders 'Analyzed' when sufficient or 'Further Details Needed' when insufficient, unpersisted round 3 candidate is never shown.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/round_two_clarification_test.dart --plain-name "Round 2 submits, analyzes, refreshes then displays sufficient=false"; Pop-Location
   */
  for (final sufficient in [false, true]) {
    testWidgets('Round 2 submits, analyzes, refreshes then displays sufficient=$sufficient', (tester) async {
      final calls = <String>[];
      var state = requestJson();
      final finishAnalysis = Completer<void>();
      final dio = Dio();
      dio.interceptors.add(InterceptorsWrapper(onRequest: (options, handler) async {
        calls.add('${options.method} ${options.path}');
        if (options.path.endsWith('/clarifications/answers')) {
          expect(options.data['clarificationRound'], 2);
          expect(options.data['answers'][0]['answer'], 'It is not cooling and makes a continuous rattling noise.');
          state = requestJson(answered: true);
          handler.resolve(Response(requestOptions: options, statusCode: 200, data: state['clarifications']));
        } else if (options.path.endsWith('/analyze')) {
          await finishAnalysis.future;
          state = requestJson(answered: true, analyzed: true, sufficient: sufficient);
          handler.resolve(Response(requestOptions: options, statusCode: 200, data: {
            'serviceRequestId': 'request-2', 'status': state['status'],
            'category': 'Appliance Repair', 'urgency': 'Medium', 'confidence': 0.85,
            'problemSummary': 'Possible refrigerator fault', 'needsMoreInformation': !sufficient,
            'followUpQuestions': sufficient ? [] : ['Unpersisted round 3 candidate'],
          }));
        } else {
          handler.resolve(Response(requestOptions: options, statusCode: 200, data: state));
        }
      }));
      final provider = ServiceRequestProvider(serviceRequestService: ServiceRequestService(apiClient: ApiClient(dio: dio)));
      await tester.runAsync(() => provider.loadRequestById('request-2'));
      calls.clear();
      await tester.pumpWidget(MaterialApp(home: Scaffold(body: SingleChildScrollView(child: ListenableBuilder(
        listenable: provider,
        builder: (context, child) {
          final request = provider.currentRequest!;
          if (request.status.name == 'analyzed') return const Text('Analyzed');
          return ClarificationSection(
            clarifications: request.clarifications,
            hasReachedMaxRounds: request.hasCompletedFinalClarificationAnalysis,
            isReanalyzing: provider.isAnalyzing, isSubmitting: provider.isLoading,
            onEditDetails: () {}, onReanalyze: () { provider.analyzeRequest('request-2'); },
            onSubmitAnswers: (round, answers) async { await provider.submitClarificationAnswersAndReanalyze('request-2', round, answers); },
          );
        },
      )))));
      expect(find.text('Round 2 of 2'), findsOneWidget);
      await tester.enterText(find.byType(TextFormField), 'It is not cooling and makes a continuous rattling noise.');
      await tester.tap(find.text('Submit & Re-analyze'));
      await tester.runAsync(() => Future<void>.delayed(const Duration(milliseconds: 50)));
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 100));
      expect(calls, ['POST /service-requests/request-2/clarifications/answers', 'POST /service-requests/request-2/analyze']);
      expect(provider.isAnalyzing, isTrue);
      expect(find.text('Further Details Needed'), findsNothing);
      finishAnalysis.complete();
      await tester.runAsync(() => Future<void>.delayed(const Duration(milliseconds: 50)));
      await tester.pumpAndSettle();
      expect(calls.last, 'GET /service-requests/request-2');
      expect(calls.where((c) => c.endsWith('/analyze')).length, 1);
      expect(find.text(sufficient ? 'Analyzed' : 'Further Details Needed'), findsOneWidget);
      expect(find.text('Unpersisted round 3 candidate'), findsNothing);
      expect(find.text('Round 3 of 2'), findsNothing);
      expect(provider.currentRequest!.clarifications.single.clarificationRound, 2);
      await tester.pumpWidget(const SizedBox());
      provider.dispose();
      dio.close();
    });
  }
}
