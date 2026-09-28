import 'package:flutter/material.dart';
import 'package:latlong2/latlong.dart';

import 'models/tracking_coordinate.dart';
import 'services/tracking_signalr_service.dart';
import 'widgets/tracking_map.dart';

class CustomerJobTrackingScreen extends StatefulWidget {
  final String jobId;
  final double destinationLatitude;
  final double destinationLongitude;

  const CustomerJobTrackingScreen({
    super.key,
    required this.jobId,
    required this.destinationLatitude,
    required this.destinationLongitude,
  });

  @override
  State<CustomerJobTrackingScreen> createState() =>
      _CustomerJobTrackingScreenState();
}

class _CustomerJobTrackingScreenState
    extends State<CustomerJobTrackingScreen> {
  late final TrackingSignalRService _signalRService;
  TrackingCoordinate? _providerLocation;
  String? _connectionError;
  bool _trackingStopped = false;

  @override
  void initState() {
    super.initState();
    _signalRService = TrackingSignalRService();
    _connect();
  }

  Future<void> _connect() async {
    try {
      await _signalRService.connect(
        jobId: widget.jobId,
        onLocation: (location) {
          if (!mounted) return;
          setState(() => _providerLocation = location);
        },
        onStopped: () {
          if (!mounted) return;
          setState(() => _trackingStopped = true);
        },
      );
    } catch (error) {
      if (!mounted) return;
      setState(() => _connectionError = error.toString());
    }
  }

  @override
  void dispose() {
    _signalRService.stop();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final destination = LatLng(
      widget.destinationLatitude,
      widget.destinationLongitude,
    );

    return Scaffold(
      appBar: AppBar(title: Text('Job #${widget.jobId} Tracking')),
      body: Column(
        children: [
          if (_connectionError != null)
            MaterialBanner(
              content: Text(_connectionError!),
              actions: [
                TextButton(onPressed: _connect, child: const Text('Retry')),
              ],
            ),
          if (_trackingStopped)
            const ListTile(
              leading: Icon(Icons.info_outline),
              title: Text('Provider tracking has stopped.'),
            ),
          Expanded(
            child: TrackingMap(
              destination: destination,
              providerLocation: _providerLocation?.point,
            ),
          ),
        ],
      ),
    );
  }
}