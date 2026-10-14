import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/service_requests/models/service_request_clarification_model.dart';
import 'package:mobile/features/service_requests/widgets/clarification_section.dart';
import 'package:mobile/shared/theme/app_theme.dart';

/**
 * TEST SUITE: Clarification Action Layout and Responsive Geometry Tests
 *
 * 1. What this test suite tests:
 *    - Verifies responsive layout, button geometry, text rendering, and accessibility of action buttons inside `ClarificationSection`.
 *    - Loads Flutter's bundled Android Roboto-Medium font in `setUpAll` to measure real text width rather than the wide test font Ahem.
 *    - Tests wide vs narrow viewports: side-by-side row layout (width 600, scale 1.0) vs vertically stacked layout (width 320 scale 1.0, width 460 scale 1.5).
 *    - Verifies 48px touch target height minimums, single-line button text without overflow, input validation error when submitting blank answers,
 *      and callback invocation for both 'Submit & Re-analyze' and 'Edit Details'.
 *    - Tests narrow loading state: buttons disabled (`onPressed == null`) and `CircularProgressIndicator` displayed while submitting.
 * 2. Why these tests are needed:
 *    - Accessibility and UX: Action buttons ('Submit & Re-analyze' and 'Edit Details') must satisfy material design touch target guidelines (>= 48px height),
 *      stack cleanly without clipping labels on compact screens or large text zoom, and enforce required answer validation before submission.
 * 3. What is real and what is mocked:
 *    - Real Flutter widgets (`ClarificationSection`, `ElevatedButton`, `OutlinedButton`, `TextFormField`, `TextPainter`, `RenderParagraph`).
 *    - Bundled Android Roboto font loaded via `FontLoader` to measure authentic typography layout.
 * 4. Prerequisites: Flutter test environment with access to FLUTTER_ROOT material fonts.
 * 5. PowerShell command to run this suite from repository root:
 *    Push-Location mobile; flutter test test/clarification_action_layout_test.dart; Pop-Location
 */
void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  setUpAll(() async {
    // Use Flutter's bundled Android font rather than the wide Ahem test font
    // when asserting that the complete action label fits its actual tap target.
    final root = Platform.environment['FLUTTER_ROOT']!;
    final font = File('$root/bin/cache/artifacts/material_fonts/roboto-medium.ttf');
    final loader = FontLoader('Roboto')
      ..addFont(Future.value(ByteData.sublistView(await font.readAsBytes())));
    await loader.load();
  });
  Future<void> showCard(WidgetTester tester, double width, {
    double scale = 1,
    bool busy = false,
    VoidCallback? edit,
    Future<void> Function(int, Map<String, String>)? submit,
  }) async {
    await tester.pumpWidget(MaterialApp(
      theme: AppTheme.lightTheme.copyWith(
        elevatedButtonTheme: ElevatedButtonThemeData(
          style: AppTheme.lightTheme.elevatedButtonTheme.style!.copyWith(
            textStyle: WidgetStatePropertyAll(
              AppTheme.lightTheme.elevatedButtonTheme.style!.textStyle!
                  .resolve({})!.copyWith(fontFamily: 'Roboto'),
            ),
          ),
        ),
      ),
      home: Scaffold(body: SingleChildScrollView(child: Align(
        alignment: Alignment.topLeft,
        child: SizedBox(width: width, child: MediaQuery(
          data: MediaQueryData(textScaler: TextScaler.linear(scale)),
          child: ClarificationSection(
            clarifications: const [ServiceRequestClarificationModel(
              id: 'q2', clarificationRound: 2, sequence: 1,
              question: 'Does it still cool?',
            )],
            onEditDetails: edit ?? () {}, onReanalyze: () {},
            onSubmitAnswers: submit, isSubmitting: busy,
          ),
        )),
      ))),
    ));
  }

  /**
   * Parameterized Responsive Geometry Tests
   * Note: Parameterized across scenarios: (600.0, 1.0, false) [wide, row], (320.0, 1.0, true) [compact, stacked], (460.0, 1.5, true) [scaled, stacked].
   * The test runner executes all 3 combinations.
   *
   * Purpose: What this specific test checks
   * - Verifies button height (>= 48px), vertical vs horizontal layout arrangement, and that the 'Submit & Re-analyze' label fits without truncation.
   *
   * Why: Why this scenario matters
   * - Ensures buttons adapt responsively to viewport constraints and large fonts without text clipping.
   *
   * Scenario: Input or condition being tested
   * - Render `ClarificationSection` under each width and text scale scenario.
   *
   * Pass condition: What result or assertion makes it pass
   * - `primary.height >= 48`, stacked buttons have `primary.bottom < secondary.top` and equal widths,
   *   side-by-side buttons share baseline top, text painter width <= paragraph width.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/clarification_action_layout_test.dart --plain-name "width 320.0, scale 1.0: readable actions and balanced layout"; Pop-Location
   */
  for (final scenario in [(600.0, 1.0, false), (320.0, 1.0, true), (460.0, 1.5, true)]) {
    testWidgets('width ${scenario.$1}, scale ${scenario.$2}: readable actions and balanced layout', (tester) async {
      await showCard(tester, scenario.$1, scale: scenario.$2);
      final primary = tester.getRect(find.byType(ElevatedButton));
      final secondary = tester.getRect(find.byType(OutlinedButton));
      expect(primary.height, greaterThanOrEqualTo(48));
      expect(primary.height, secondary.height);
      if (scenario.$3) {
        expect(primary.bottom, lessThan(secondary.top));
        expect(primary.width, secondary.width);
        expect(primary.left, secondary.left);
      } else {
        expect(primary.top, secondary.top);
        expect(primary.left, greaterThan(secondary.right));
        expect(primary.width, greaterThan(secondary.width));
      }
      final label = find.text('Submit & Re-analyze');
      expect(tester.widget<Text>(label).maxLines, 1);
      final paragraph = tester.renderObject<RenderParagraph>(label);
      final painter = TextPainter(text: paragraph.text,
        textDirection: TextDirection.ltr, textScaler: paragraph.textScaler)..layout();
      expect(painter.width, lessThanOrEqualTo(paragraph.size.width + 0.1));
      painter.dispose();
      expect(tester.takeException(), isNull);
      expect(find.text('Round 2 of 2'), findsOneWidget);
      expect(find.text('Further Details Needed'), findsNothing);
    });
  }

  /**
   * Parameterized Form Validation and Callback Tests
   * Note: Parameterized across widths [320.0, 600.0]. The test runner executes both cases.
   *
   * Purpose: What this specific test checks
   * - Verifies form validation on blank input, submission callback with round and answers, and edit callback across both narrow and wide layouts.
   *
   * Why: Why this scenario matters
   * - Ensures validation logic and event handling remain intact regardless of responsive layout transformations.
   *
   * Scenario: Input or condition being tested
   * - Tap Submit without text (expect validation error), enter text, tap Submit (expect submission callback), tap Edit Details (expect edit callback).
   *
   * Pass condition: What result or assertion makes it pass
   * - Empty submit blocks callback and shows 'Please provide an answer to this question.',
   *   valid submit calls `onSubmitAnswers` with round 2 and answers map, tapping 'Edit Details' calls `onEditDetails`.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/clarification_action_layout_test.dart --plain-name "width 320.0 preserves validation and both callbacks"; Pop-Location
   */
  for (final width in [320.0, 600.0]) {
    testWidgets('width $width preserves validation and both callbacks', (tester) async {
      var edits = 0;
      var submissions = 0;
      await showCard(tester, width, edit: () => edits++, submit: (round, answers) async {
        expect(round, 2);
        expect(answers, {'q2': 'It no longer cools.'});
        submissions++;
      });
      await tester.tap(find.text('Submit & Re-analyze'));
      await tester.pumpAndSettle();
      expect(submissions, 0);
      expect(find.text('Please provide an answer to this question.'), findsOneWidget);
      await tester.enterText(find.byType(TextFormField), 'It no longer cools.');
      await tester.ensureVisible(find.text('Submit & Re-analyze'));
      await tester.tap(find.text('Submit & Re-analyze'));
      await tester.pumpAndSettle();
      expect(submissions, 1);
      await tester.ensureVisible(find.text('Edit Details'));
      await tester.tap(find.text('Edit Details'));
      expect(edits, 1);
      expect(tester.takeException(), isNull);
    });
  }

  /**
   * Purpose: What this specific test checks
   * - Verifies that when `isSubmitting: true`, buttons are disabled and a loading spinner appears on narrow screens.
   *
   * Why: Why this scenario matters
   * - Prevents double-taps and confirms progress to the customer during network transmission.
   *
   * Scenario: Input or condition being tested
   * - Render `ClarificationSection` with `busy: true` at width 320px.
   *
   * Pass condition: What result or assertion makes it pass
   * - `ElevatedButton.onPressed` is null, `OutlinedButton.onPressed` is null, `CircularProgressIndicator` is found, 'Round 2 of 2' header remains visible.
   *
   * Run from repository root:
   * Push-Location mobile; flutter test test/clarification_action_layout_test.dart --plain-name "narrow loading state preserves disabled actions and pending round"; Pop-Location
   */
  testWidgets('narrow loading state preserves disabled actions and pending round', (tester) async {
    await showCard(tester, 320, busy: true);
    expect(tester.widget<ElevatedButton>(find.byType(ElevatedButton)).onPressed, isNull);
    expect(tester.widget<OutlinedButton>(find.byType(OutlinedButton)).onPressed, isNull);
    expect(find.byType(CircularProgressIndicator), findsOneWidget);
    expect(find.text('Round 2 of 2'), findsOneWidget);
    expect(find.text('Further Details Needed'), findsNothing);
    expect(tester.takeException(), isNull);
  });
}
