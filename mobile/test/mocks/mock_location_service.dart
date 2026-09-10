import 'package:mobile/features/service_requests/services/location_service.dart';

class MockLocationService implements LocationService {
  bool serviceEnabled = true;
  LocationAccessStatus permissionStatus = LocationAccessStatus.granted;
  LocationResult? customResult;
  bool shouldThrow = false;
  Exception? throwException;
  int getCurrentLocationCallCount = 0;

  @override
  Future<bool> isLocationServiceEnabled() async => serviceEnabled;

  @override
  Future<LocationAccessStatus> checkPermission() async => permissionStatus;

  @override
  Future<LocationAccessStatus> requestPermission() async => permissionStatus;

  @override
  Future<LocationResult> getCurrentLocation({
    Duration timeLimit = const Duration(seconds: 10),
  }) async {
    getCurrentLocationCallCount++;
    if (shouldThrow) {
      throw throwException ?? Exception('Simulated GPS platform failure');
    }
    if (customResult != null) {
      return customResult!;
    }
    if (!serviceEnabled) {
      return const LocationResult(
        status: LocationAccessStatus.servicesDisabled,
        message: 'Location services are turned off. You can enter the address manually.',
      );
    }
    if (permissionStatus == LocationAccessStatus.denied) {
      return const LocationResult(
        status: LocationAccessStatus.denied,
        message: 'Location permission was denied. You can enter the service location manually.',
      );
    }
    if (permissionStatus == LocationAccessStatus.deniedForever) {
      return const LocationResult(
        status: LocationAccessStatus.deniedForever,
        message: 'Location access is disabled for AssistLK. Enable it in device settings or enter the location manually.',
      );
    }
    return const LocationResult(
      status: LocationAccessStatus.granted,
      coordinates: LocationCoordinates(
        latitude: 6.9271,
        longitude: 79.8612,
      ),
      message: 'GPS location captured',
    );
  }
}
