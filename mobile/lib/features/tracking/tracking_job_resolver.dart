import '../../core/api/api_client.dart';

/// Resolves the ServiceJob id for an accepted dispatch match (MatchedCandidate id).
/// Calls POST /service-jobs/resolve-from-match, which finds or creates the ServiceJob.
Future<String?> resolveServiceJobId(ApiClient apiClient, String matchId) async {
  final response = await apiClient.client.post(
    '/service-jobs/resolve-from-match',
    data: {'matchId': matchId},
  );
  final data = response.data;
  if (data is Map) {
    return data['serviceJobId']?.toString();
  }
  return null;
}
/// Reads the real status of a ServiceJob from the server (GET /service-jobs/{id}).
Future<String?> fetchServiceJobStatus(
  ApiClient apiClient,
  String serviceJobId,
) async {
  final response = await apiClient.client.get('/service-jobs/$serviceJobId');
  final data = response.data;
  if (data is Map) {
    return data['status']?.toString();
  }
  return null;
}