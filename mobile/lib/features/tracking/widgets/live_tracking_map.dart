import 'package:flutter/material.dart';
import 'package:latlong2/latlong.dart';

import '../models/tracking_coordinate.dart';
import '../services/tracking_signalr_service.dart';
import 'tracking_map.dart';

class LiveTrackingMap extends StatefulWidget {
  final String jobId;
  final String status;
  final LatLng destination;
  final TrackingStoppedCallback? onTrackingStopped;
  final ValueChanged<String>? onConnectionError;

  const LiveTrackingMap({
    super.key,
    required this.jobId,
    required this.status,
    required this.destination,
    this.onTrackingStopped,
    this.onConnectionError,
  });

  @override
  State<LiveTrackingMap> createState() => _LiveTrackingMapState();
}

class _LiveTrackingMapState extends State<LiveTrackingMap> {
  late final TrackingSignalRService _signalRService;
  TrackingCoordinate? _providerLocation;

  @override
  void initState() {
    super.initState();
    _signalRService = TrackingSignalRService();
    _connectIfOnTheWay();
  }

  @override
  void didUpdateWidget(covariant LiveTrackingMap oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.jobId != widget.jobId || oldWidget.status != widget.status) {
      _connectIfOnTheWay();
    }
  }

  Future<void> _connectIfOnTheWay() async {
    await _signalRService.stop();
    if (widget.status != 'OnTheWay') return;

    try {
      await _signalRService.connect(
        jobId: widget.jobId,
        onLocation: (location) {
          if (mounted) setState(() => _providerLocation = location);
        },
        onStopped: widget.onTrackingStopped,
      );
    } catch (error) {
      widget.onConnectionError?.call(error.toString());
    }
  }

  @override
  void dispose() {
    _signalRService.stop();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return TrackingMap(
      destination: widget.destination,
      providerLocation: _providerLocation?.point,
    );
  }
}
