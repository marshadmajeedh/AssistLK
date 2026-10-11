import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';

import '../../../shared/theme/app_colors.dart';

class TrackingMap extends StatefulWidget {
  final LatLng destination;
  final LatLng? providerLocation;

  const TrackingMap({
    super.key,
    required this.destination,
    this.providerLocation,
  });

  @override
  State<TrackingMap> createState() => _TrackingMapState();
}

class _TrackingMapState extends State<TrackingMap> {
  final MapController _mapController = MapController();
  bool _mapReady = false;

  @override
  void didUpdateWidget(covariant TrackingMap oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.providerLocation != widget.providerLocation &&
        widget.providerLocation != null) {
      _fitLocations();
    }
  }

  void _fitLocations() {
    if (!_mapReady || widget.providerLocation == null) return;

    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted || !_mapReady || widget.providerLocation == null) return;
      final providerLocation = widget.providerLocation!;
      if (providerLocation.latitude == widget.destination.latitude &&
          providerLocation.longitude == widget.destination.longitude) {
        _mapController.move(providerLocation, 15);
        return;
      }

      try {
        _mapController.fitCamera(
          CameraFit.bounds(
            bounds: LatLngBounds.fromPoints([
              widget.destination,
              providerLocation,
            ]),
            padding: const EdgeInsets.all(64),
          ),
        );
      } catch (_) {
        // Keep the existing camera if the map is not ready to fit bounds.
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    final markers = <Marker>[
      Marker(
        point: widget.destination,
        width: 48,
        height: 48,
        child: const Icon(
          Icons.home_rounded,
          color: AppColors.primary,
          size: 40,
        ),
      ),
      if (widget.providerLocation != null)
        Marker(
          point: widget.providerLocation!,
          width: 48,
          height: 48,
          child: const Icon(
            Icons.person_pin_circle_rounded,
            color: AppColors.primary,
            size: 42,
          ),
        ),
    ];

    return Stack(
      children: [
        FlutterMap(
          mapController: _mapController,
          options: MapOptions(
            initialCenter: widget.providerLocation ?? widget.destination,
            initialZoom: 14,
            onMapReady: () {
              _mapReady = true;
              _fitLocations();
            },
          ),
          children: [
            TileLayer(
              urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
              userAgentPackageName: 'com.assistlk.mobile',
            ),
            if (widget.providerLocation != null)
              PolylineLayer(
                polylines: [
                  Polyline(
                    points: [widget.providerLocation!, widget.destination],
                    strokeWidth: 4,
                    color: AppColors.primary,
                  ),
                ],
              ),
            MarkerLayer(markers: markers),
          ],
        ),
        Positioned(
          top: 14,
          left: 14,
          child: Container(
            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
            decoration: BoxDecoration(
              color: AppColors.surface,
              borderRadius: BorderRadius.circular(14),
              border: Border.all(color: AppColors.border),
              boxShadow: const [
                BoxShadow(
                  color: Color(0x1A000000),
                  blurRadius: 12,
                  offset: Offset(0, 3),
                ),
              ],
            ),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Icon(Icons.schedule_rounded, color: AppColors.primary),
                const SizedBox(width: 8),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Text(
                      'ETA',
                      style: TextStyle(
                        color: AppColors.textSecondary,
                        fontSize: 11,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                    Text(
                      widget.providerLocation == null
                          ? 'Waiting for agent location'
                          : 'Live location updating',
                      style: const TextStyle(
                        color: AppColors.primaryDark,
                        fontSize: 12,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }
}