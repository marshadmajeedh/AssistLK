import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';

class TrackingMap extends StatelessWidget {
  final LatLng destination;
  final LatLng? providerLocation;

  const TrackingMap({
    super.key,
    required this.destination,
    this.providerLocation,
  });

  @override
  Widget build(BuildContext context) {
    final markers = <Marker>[
      Marker(
        point: destination,
        width: 48,
        height: 48,
        child: const Icon(Icons.location_pin, color: Colors.red, size: 44),
      ),
      if (providerLocation != null)
        Marker(
          point: providerLocation!,
          width: 48,
          height: 48,
          child: const Icon(
            Icons.directions_car,
            color: Colors.blue,
            size: 34,
          ),
        ),
    ];

    return FlutterMap(
      options: MapOptions(
        initialCenter: providerLocation ?? destination,
        initialZoom: 14,
      ),
      children: [
        TileLayer(
          urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
          userAgentPackageName: 'com.assistlk.mobile',
        ),
        MarkerLayer(markers: markers),
      ],
    );
  }
}