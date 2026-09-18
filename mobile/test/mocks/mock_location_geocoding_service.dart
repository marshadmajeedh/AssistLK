import 'package:mobile/core/api/api_client.dart';
import 'package:mobile/features/service_requests/models/resolved_location.dart';
import 'package:mobile/features/service_requests/services/location_geocoding_service.dart';

class MockLocationGeocodingService extends LocationGeocodingService {
  MockLocationGeocodingService() : super(apiClient: ApiClient());
  int calls = 0;
  double? latitude, longitude;
  Future<ResolvedLocation> Function()? reply;
  @override
  Future<ResolvedLocation> reverseGeocode(
    double latitude,
    double longitude,
  ) async {
    calls++;
    this.latitude = latitude;
    this.longitude = longitude;
    return reply != null
        ? reply!()
        : const ResolvedLocation(formattedAddress: 'Example Road, Kotte');
  }
}
