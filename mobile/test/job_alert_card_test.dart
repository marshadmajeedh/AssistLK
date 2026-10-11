import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/providers/widgets/job_alert_card.dart';

Widget _buildTestCard({
  String category = 'Plumbing',
  String distance = '3.5 km',
  String urgency = 'High',
  String? description,
  String? aiRationale,
  bool isOutOfRange = false,
  int? remainingSeconds,
  int totalTimeoutSeconds = 60,
  VoidCallback? onAccept,
  VoidCallback? onDecline,
}) {
  return MaterialApp(
    theme: ThemeData(
      useMaterial3: true,
      primaryColor: Colors.blue,
    ),
    home: Scaffold(
      body: Center(
        child: SingleChildScrollView(
          child: JobAlertCard(
            category: category,
            distance: distance,
            urgency: urgency,
            description: description,
            aiRationale: aiRationale,
            isOutOfRange: isOutOfRange,
            remainingSeconds: remainingSeconds,
            totalTimeoutSeconds: totalTimeoutSeconds,
            onAccept: onAccept,
            onDecline: onDecline,
          ),
        ),
      ),
    ),
  );
}

void main() {
  group('JobAlertCard Widget Tests (Component 2: AI Matching & Dispatch)', () {
    testWidgets('renders basic job alert card with category, distance, and high urgency',
        (tester) async {
      await tester.pumpWidget(
        _buildTestCard(
          category: 'Plumbing',
          distance: '3.5 km',
          urgency: 'High',
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Plumbing'), findsOneWidget);
      expect(find.text('3.5 km away'), findsOneWidget);
      expect(find.text('High'), findsOneWidget);

      // Verify buttons
      expect(find.text('Decline'), findsOneWidget);
      expect(find.text('Accept Match'), findsOneWidget);
      expect(find.byIcon(Icons.location_on), findsOneWidget);
    });

    testWidgets('renders standard urgency with orange styling', (tester) async {
      await tester.pumpWidget(
        _buildTestCard(
          urgency: 'Standard',
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Standard'), findsOneWidget);

      final urgencyText = tester.widget<Text>(find.text('Standard'));
      expect(urgencyText.style?.color, Colors.orange.shade900);
    });

    testWidgets('renders customer problem description when provided',
        (tester) async {
      await tester.pumpWidget(
        _buildTestCard(
          description: 'Water pipe leaking beneath the kitchen counter.',
        ),
      );
      await tester.pumpAndSettle();

      expect(
        find.text('Customer: "Water pipe leaking beneath the kitchen counter."'),
        findsOneWidget,
      );
    });

    testWidgets('renders AI Problem Review box when aiRationale is supplied',
        (tester) async {
      const rationale =
          'Provider is ranked #1 (Score: 92.4) due to verified plumbing license and 3.5km proximity.';
      await tester.pumpWidget(
        _buildTestCard(
          aiRationale: rationale,
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('AI Problem Review'), findsOneWidget);
      expect(find.byIcon(Icons.auto_awesome), findsOneWidget);
      expect(find.text(rationale), findsOneWidget);
    });

    testWidgets('omits AI Problem Review box when aiRationale is null or empty',
        (tester) async {
      await tester.pumpWidget(
        _buildTestCard(
          aiRationale: null,
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('AI Problem Review'), findsNothing);
      expect(find.byIcon(Icons.auto_awesome), findsNothing);
    });

    testWidgets('renders countdown timer and linear progress bar when remainingSeconds is set',
        (tester) async {
      await tester.pumpWidget(
        _buildTestCard(
          remainingSeconds: 45,
          totalTimeoutSeconds: 60,
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('45s'), findsOneWidget);
      expect(find.byIcon(Icons.timer_outlined), findsOneWidget);
      expect(find.byType(LinearProgressIndicator), findsOneWidget);
      expect(find.text('Accept Match (45s)'), findsOneWidget);
    });

    testWidgets('highlights countdown in red when remainingSeconds <= 15',
        (tester) async {
      await tester.pumpWidget(
        _buildTestCard(
          remainingSeconds: 10,
          totalTimeoutSeconds: 60,
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('10s'), findsOneWidget);
      final timerText = tester.widget<Text>(find.text('10s'));
      expect(timerText.style?.color, Colors.red.shade900);
    });

    testWidgets('enforces out-of-radius boundary guard by disabling Accept Match',
        (tester) async {
      bool acceptTapped = false;
      await tester.pumpWidget(
        _buildTestCard(
          distance: '18.5 km',
          isOutOfRange: true,
          onAccept: () => acceptTapped = true,
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Out of radius (18.5 km)'), findsOneWidget);

      // Verify Card border is amber
      final card = tester.widget<Card>(find.byType(Card));
      final shape = card.shape as RoundedRectangleBorder;
      expect(shape.side.color, Colors.amber);

      // Verify Accept button is disabled
      final acceptButton = tester.widget<ElevatedButton>(find.byType(ElevatedButton));
      expect(acceptButton.onPressed, isNull);

      // Attempt tapping disabled button
      await tester.tap(find.byType(ElevatedButton));
      await tester.pumpAndSettle();
      expect(acceptTapped, isFalse);
    });

    testWidgets('triggers onAccept callback when Accept button is tapped within radius',
        (tester) async {
      bool acceptTapped = false;
      await tester.pumpWidget(
        _buildTestCard(
          onAccept: () => acceptTapped = true,
        ),
      );
      await tester.pumpAndSettle();

      final acceptBtn = find.text('Accept Match');
      expect(acceptBtn, findsOneWidget);
      await tester.tap(acceptBtn);
      await tester.pumpAndSettle();

      expect(acceptTapped, isTrue);
    });

    testWidgets('triggers onDecline callback when Decline button is tapped',
        (tester) async {
      bool declineTapped = false;
      await tester.pumpWidget(
        _buildTestCard(
          onDecline: () => declineTapped = true,
        ),
      );
      await tester.pumpAndSettle();

      final declineBtn = find.text('Decline');
      expect(declineBtn, findsOneWidget);
      await tester.tap(declineBtn);
      await tester.pumpAndSettle();

      expect(declineTapped, isTrue);
    });
  });
}
