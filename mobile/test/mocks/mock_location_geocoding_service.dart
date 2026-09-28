import 'package:mobile/core/api/api_client.dart';
import 'package:mobile/features/service_requests/models/forward_geocode_candidate.dart';
import 'package:mobile/features/service_requests/models/resolved_location.dart';
import 'package:mobile/features/service_requests/services/location_geocoding_service.dart';

class MockLocationGeocodingService extends LocationGeocodingService {
  MockLocationGeocodingService() : super(apiClient: ApiClient());
  int calls = 0;
  double? latitude, longitude;
  Future<ResolvedLocation> Function()? reply;

  int forwardCalls = 0;
  String? forwardAddress;
  Future<List<ForwardGeocodeCandidate>> Function(String address)? forwardReply;

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

  @override
  Future<List<ForwardGeocodeCandidate>> forwardGeocode(String address) async {
    forwardCalls++;
    forwardAddress = address;
    if (forwardReply != null) {
      return forwardReply!(address);
    }
    return [
      ForwardGeocodeCandidate(
        displayAddress: address,
        latitude: 6.905,
        longitude: 79.86,
        placeId: '101',
        source: 'OpenStreetMap',
      ),
    ];
  }
}
