import 'dart:async';

import 'package:geolocator/geolocator.dart';

class LocationCoordinates {
  final double latitude;
  final double longitude;
  final double? accuracy;

  const LocationCoordinates({
    required this.latitude,
    required this.longitude,
    this.accuracy,
  });

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is LocationCoordinates &&
          runtimeType == other.runtimeType &&
          latitude == other.latitude &&
          longitude == other.longitude;

  @override
  int get hashCode => Object.hash(latitude, longitude);

  @override
  String toString() => 'LocationCoordinates(latitude: $latitude, longitude: $longitude)';
}

enum LocationAccessStatus {
  granted,
  denied,
  deniedForever,
  servicesDisabled,
  timeout,
  error,
}

class LocationResult {
  final LocationAccessStatus status;
  final LocationCoordinates? coordinates;
  final String? message;

  const LocationResult({
    required this.status,
    this.coordinates,
    this.message,
  });

  bool get isSuccess =>
      status == LocationAccessStatus.granted && coordinates != null;
}

abstract class LocationService {
  Future<bool> isLocationServiceEnabled();
  Future<LocationAccessStatus> checkPermission();
  Future<LocationAccessStatus> requestPermission();
  Future<LocationResult> getCurrentLocation({
    Duration timeLimit = const Duration(seconds: 10),
  });
}

class GeolocatorLocationService implements LocationService {
  @override
  Future<bool> isLocationServiceEnabled() async {
    try {
      return await Geolocator.isLocationServiceEnabled();
    } catch (_) {
      return false;
    }
  }

  @override
  Future<LocationAccessStatus> checkPermission() async {
    try {
      final perm = await Geolocator.checkPermission();
      return _mapPermission(perm);
    } catch (_) {
      return LocationAccessStatus.error;
    }
  }

  @override
  Future<LocationAccessStatus> requestPermission() async {
    try {
      final perm = await Geolocator.requestPermission();
      return _mapPermission(perm);
    } catch (_) {
      return LocationAccessStatus.error;
    }
  }

  @override
  Future<LocationResult> getCurrentLocation({
    Duration timeLimit = const Duration(seconds: 10),
  }) async {
    try {
      final serviceEnabled = await Geolocator.isLocationServiceEnabled();
      if (!serviceEnabled) {
        return const LocationResult(
          status: LocationAccessStatus.servicesDisabled,
          message:
              'Location services are turned off. You can enter the address manually.',
        );
      }

      var permission = await Geolocator.checkPermission();
      if (permission == LocationPermission.denied) {
        permission = await Geolocator.requestPermission();
        if (permission == LocationPermission.denied) {
          return const LocationResult(
            status: LocationAccessStatus.denied,
            message:
                'Location permission was denied. You can enter the service location manually.',
          );
        }
      }

      if (permission == LocationPermission.deniedForever) {
        return const LocationResult(
          status: LocationAccessStatus.deniedForever,
          message:
              'Location access is disabled for AssistLK. Enable it in device settings or enter the location manually.',
        );
      }

      final position = await Geolocator.getCurrentPosition(
        locationSettings: LocationSettings(
          accuracy: LocationAccuracy.high,
          timeLimit: timeLimit,
        ),
      );

      return LocationResult(
        status: LocationAccessStatus.granted,
        coordinates: LocationCoordinates(
          latitude: position.latitude,
          longitude: position.longitude,
          accuracy: position.accuracy,
        ),
        message: 'GPS location captured',
      );
    } on TimeoutException {
      return const LocationResult(
        status: LocationAccessStatus.timeout,
        message:
            'Could not get your current location in time. Please enter the location manually.',
      );
    } catch (_) {
      return const LocationResult(
        status: LocationAccessStatus.error,
        message:
            'Could not retrieve your current location. Please enter the location manually.',
      );
    }
  }

  static LocationAccessStatus _mapPermission(LocationPermission permission) {
    switch (permission) {
      case LocationPermission.always:
      case LocationPermission.whileInUse:
        return LocationAccessStatus.granted;
      case LocationPermission.denied:
      case LocationPermission.unableToDetermine:
        return LocationAccessStatus.denied;
      case LocationPermission.deniedForever:
        return LocationAccessStatus.deniedForever;
    }
  }
}
