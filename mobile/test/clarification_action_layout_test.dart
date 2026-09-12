import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/service_requests/models/service_request_clarification_model.dart';
import 'package:mobile/features/service_requests/widgets/clarification_section.dart';
import 'package:mobile/shared/theme/app_theme.dart';

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
