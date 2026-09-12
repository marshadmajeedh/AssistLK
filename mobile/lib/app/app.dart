import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../features/auth/models/auth_user.dart';
import '../features/auth/providers/auth_provider.dart';
import '../features/auth/screens/auth_gate.dart';
import '../features/customer/providers/customer_location_provider.dart';
import '../features/service_requests/services/location_service.dart';
import '../features/service_requests/services/location_geocoding_service.dart';
import '../features/service_requests/providers/service_request_provider.dart';
import '../features/service_requests/services/service_request_service.dart';
import '../shared/theme/app_theme.dart';

/// Owns customer state and the entire Navigator for one authenticated session.
class AssistLKApp extends StatefulWidget {
  const AssistLKApp({
    super.key,
    this.serviceRequestService,
    this.locationService,
    this.geocodingService,
    this.locationClock,
  });
  final ServiceRequestService? serviceRequestService;
  final LocationService? locationService;
  final LocationGeocodingService? geocodingService;
  final DateTime Function()? locationClock;
  @override
  State<AssistLKApp> createState() => _AssistLKAppState();
}

class _AssistLKAppState extends State<AssistLKApp> {
  AuthProvider? _auth;
  AuthUser? _sessionUser;
  ServiceRequestProvider? _requests;
  CustomerLocationProvider? _location;
  int _session = 0;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final auth = context.read<AuthProvider>();
    if (identical(auth, _auth)) return;
    _auth?.removeListener(_authChanged);
    _auth = auth;
    _auth!.addListener(_authChanged);
    _replaceSession();
  }

  void _authChanged() {
    if (identical(_sessionUser, _auth!.user)) return;
    setState(_replaceSession);
  }

  void _replaceSession() {
    // Invalidate pending work before the old routes are unmounted.
    _requests?.reset();
    _requests?.dispose();
    _location?.reset();
    _location?.dispose();
    _sessionUser = _auth!.user;
    _session++;
    _requests = _sessionUser?.role == 'Customer'
        ? ServiceRequestProvider(
            serviceRequestService:
                widget.serviceRequestService ??
                ServiceRequestService(apiClient: _auth!.authService.apiClient),
          )
        : null;
    _location = _sessionUser?.role == 'Customer'
        ? CustomerLocationProvider(
            gps: widget.locationService ?? GeolocatorLocationService(),
            geocoding:
                widget.geocodingService ??
                LocationGeocodingService(
                  apiClient: _auth!.authService.apiClient,
                ),
            clock: widget.locationClock,
          )
        : null;
  }

  @override
  void dispose() {
    _auth?.removeListener(_authChanged);
    _requests?.dispose();
    _location?.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final app = MaterialApp(
      key: ValueKey(_session),
      title: 'AssistLK',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.lightTheme,
      home: const AuthGate(),
    );
    final requests = _requests;
    return requests == null
        ? app
        : MultiProvider(
            providers: [
              ChangeNotifierProvider<ServiceRequestProvider>.value(
                value: requests,
              ),
              ChangeNotifierProvider<CustomerLocationProvider>.value(
                value: _location!,
              ),
            ],
            child: app,
          );
  }
}
