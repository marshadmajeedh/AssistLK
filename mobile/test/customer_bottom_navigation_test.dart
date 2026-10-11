import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/app/customer_bottom_navigation.dart';
import 'package:mobile/shared/theme/app_colors.dart';
import 'package:mobile/shared/theme/app_theme.dart';

void main() {
  for (final width in [320.0, 393.0]) {
    for (final scale in [1.0, 2.0]) {
      testWidgets(
        'floating navigation fits $width at scale $scale with safe area',
        (tester) async {
          tester.view.physicalSize = Size(width, 852);
          tester.view.devicePixelRatio = 1;
          tester.view.padding = const FakeViewPadding(bottom: 24);
          tester.platformDispatcher.textScaleFactorTestValue = scale;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);
          addTearDown(tester.view.resetPadding);
          addTearDown(tester.platformDispatcher.clearTextScaleFactorTestValue);
          var selected = 0;
          await tester.pumpWidget(
            MaterialApp(
              theme: AppTheme.lightTheme,
              home: StatefulBuilder(
                builder: (context, setState) => Scaffold(
                  body: const SizedBox.expand(key: Key('page_content')),
                  bottomNavigationBar: CustomerBottomNavigation(
                    selectedIndex: selected,
                    onDestinationSelected: (index) =>
                        setState(() => selected = index),
                  ),
                ),
              ),
            ),
          );
          for (var index = 0; index < 4; index++) {
            final label = CustomerBottomNavigation.labels[index];
            final item = find.byKey(ValueKey('customer_navigation_$label'));
            expect(item, findsOneWidget);
            expect(tester.getSize(item).width, greaterThanOrEqualTo(48));
            expect(tester.getSize(item).height, greaterThanOrEqualTo(48));
            expect(
              tester
                  .renderObject<RenderParagraph>(find.text(label))
                  .didExceedMaxLines,
              isFalse,
            );
            await tester.tap(item);
            await tester.pumpAndSettle();
            expect(selected, index);
            for (var other = 0; other < 4; other++) {
              final semantics = tester.widget<Semantics>(
                find.byKey(
                  ValueKey(
                    'customer_navigation_${CustomerBottomNavigation.labels[other]}',
                  ),
                ),
              );
              expect(semantics.properties.selected, other == index);
              expect(semantics.properties.button, isTrue);
              final pill = tester.widget<AnimatedContainer>(
                find.byKey(ValueKey('customer_navigation_pill_$other')),
              );
              expect(
                (pill.decoration! as BoxDecoration).color,
                other == index ? AppColors.primary : Colors.transparent,
              );
            }
          }
          final surface = tester.getRect(
            find.byKey(const Key('floating_navigation_surface')),
          );
          expect(surface.left, 24);
          expect(surface.right, width - 24);
          expect(surface.bottom, 852 - 24 - 12);
          expect(
            tester.getRect(find.byKey(const Key('page_content'))).bottom,
            lessThanOrEqualTo(surface.top),
          );
          expect(tester.takeException(), isNull);
        },
      );
    }
  }
}
