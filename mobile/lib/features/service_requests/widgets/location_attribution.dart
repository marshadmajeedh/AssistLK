import 'package:flutter/material.dart';

/// Text attribution for compact address containers; never an imitation logo.
class LocationAttribution extends StatelessWidget {
  const LocationAttribution({super.key});
  @override
  Widget build(BuildContext context) => const Padding(
    padding: EdgeInsets.only(top: 8),
    child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text(
      '\u00a9 OpenStreetMap contributors',
      style: TextStyle(
        fontSize: 12,
        fontWeight: FontWeight.w400,
        fontStyle: FontStyle.normal,
        color: Color(0xFF1F1F1F),
      ),
    ),
      SelectableText('Open Database License (ODbL)\nhttps://www.openstreetmap.org/copyright',
        style: TextStyle(fontSize: 12, color: Color(0xFF1F1F1F))),
    ]),
  );
}
