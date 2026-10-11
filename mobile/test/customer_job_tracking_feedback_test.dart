import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/tracking/customer_job_tracking_screen.dart';

Widget _buildScreen({required bool hasFeedback}) {
  return MaterialApp(
    home: CustomerJobTrackingScreen(
      jobId: 'job-1',
      status: 'Completed',
      destinationLatitude: 6.9271,
      destinationLongitude: 79.8612,
      hasFeedback: hasFeedback,
    ),
  );
}

void main() {
  testWidgets('Completed job without feedback shows the feedback form',
      (tester) async {
    await tester.pumpWidget(_buildScreen(hasFeedback: false));
    await tester.pump();

    expect(find.text('How was your experience?'), findsOneWidget);
    expect(find.byIcon(Icons.star), findsWidgets);
    expect(find.byType(TextField), findsOneWidget);
    expect(find.text('Feedback submitted. Thank you.'), findsNothing);
  });

  testWidgets('Completed job with feedback shows submitted message only',
      (tester) async {
    await tester.pumpWidget(_buildScreen(hasFeedback: true));
    await tester.pump();

    expect(find.text('Feedback submitted. Thank you.'), findsOneWidget);
    expect(find.text('How was your experience?'), findsNothing);
    expect(find.byType(TextField), findsNothing);
  });

  testWidgets('Selecting a star updates the rating icons', (tester) async {
    await tester.pumpWidget(_buildScreen(hasFeedback: false));
    await tester.pump();

    // Default rating is 5, so all 5 stars are filled.
    expect(find.byIcon(Icons.star), findsNWidgets(5));

    await tester.ensureVisible(find.byIcon(Icons.star).first);
    await tester.tap(find.byIcon(Icons.star).first);
    await tester.pump();

    // Rating 1 selected: 1 filled star, 4 outlined.
    expect(find.byIcon(Icons.star), findsOneWidget);
    expect(find.byIcon(Icons.star_border), findsNWidgets(4));
  });
}