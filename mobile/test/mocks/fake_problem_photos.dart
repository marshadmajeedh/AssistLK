import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:image_picker/image_picker.dart';
import 'package:mobile/core/api/api_client.dart';
import 'package:mobile/core/auth/token_storage.dart';
import 'package:mobile/features/auth/models/auth_user.dart';
import 'package:mobile/features/auth/providers/auth_provider.dart';
import 'package:mobile/features/auth/services/auth_service.dart';
import 'package:mobile/features/service_requests/models/create_service_request_dto.dart';
import 'package:mobile/features/service_requests/models/problem_photo.dart';
import 'package:mobile/features/service_requests/models/service_request_attachment.dart';
import 'package:mobile/features/service_requests/models/service_request_model.dart';
import 'package:mobile/features/service_requests/models/service_request_status.dart';
import 'package:mobile/features/service_requests/models/service_request_urgency.dart';
import 'package:mobile/features/service_requests/services/problem_image_picker.dart';
import 'package:mobile/features/service_requests/services/service_request_service.dart';

final photoBytes = base64Decode(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg==',
);
ProblemPhoto photo(
  int i, {
  PhotoSource source = PhotoSource.gallery,
  String extension = 'png',
  Uint8List? bytes,
}) => ProblemPhoto(
  file: XFile.fromData(
    bytes ?? photoBytes,
    name: 'photo$i.$extension',
    path: 'photo$i.$extension',
  ),
  source: source,
);

class FakePhotoPicker extends ProblemImagePicker {
  ProblemPhoto? next;
  Completer<ProblemPhoto?>? pending;
  Object? error;
  final List<PhotoSource> calls = [];
  List<ProblemPhoto> recovered = [];
  @override
  Future<ProblemPhoto?> pick(
    PhotoSource source, {
    required String owner,
    required String scope,
  }) async {
    calls.add(source);
    if (error != null) throw error!;
    return pending != null ? pending!.future : next;
  }

  @override
  Future<List<ProblemPhoto>> recover({
    required String owner,
    required String scope,
  }) async => recovered;
}

class MemoryTokens extends TokenStorage {
  String? token = 'old-token';
  @override
  Future<String?> getToken() async => token;
  @override
  Future<void> saveToken(String value) async {
    generation++;
    token = value;
  }

  @override
  Future<void> deleteToken() async {
    generation++;
    token = null;
  }
}

class PhotoAuth extends AuthProvider {
  PhotoAuth()
    : super(
        authService: AuthService(apiClient: ApiClient()),
        tokenStorage: MemoryTokens(),
      );
  AuthUser? current = const AuthUser(
    userId: 'customer',
    fullName: 'Customer',
    email: 'test@example.test',
    role: 'Customer',
  );
  int version = 1;
  @override
  AuthUser? get user => current;
  @override
  int get sessionGeneration => version;
  void change(String? id) {
    version++;
    current = id == null
        ? null
        : AuthUser(
            userId: id,
            fullName: id,
            email: '$id@example.test',
            role: 'Customer',
          );
    notifyListeners();
  }
}

ServiceRequestModel request({
  ServiceRequestStatus status = ServiceRequestStatus.created,
}) => ServiceRequestModel(
  serviceRequestId: 'request-1',
  customerId: 'customer',
  category: 'Plumbing',
  description: 'Water is leaking under the kitchen sink',
  locationText: 'Colombo',
  urgency: ServiceRequestUrgency.medium,
  status: status,
  createdAt: DateTime(2026),
  updatedAt: DateTime(2026),
);
ServiceRequestAttachment attachment(int i) => ServiceRequestAttachment(
  id: 'attachment-$i',
  slot: i,
  contentType: 'image/jpeg',
  fileSizeBytes: photoBytes.length,
  width: 1,
  height: 1,
  createdAt: DateTime(2026),
);
DioException photoFailure(int status) => DioException(
  requestOptions: RequestOptions(path: '/photos'),
  response: Response(
    requestOptions: RequestOptions(path: '/photos'),
    statusCode: status,
    data: {'message': 'private server path /secret/filename stack trace'},
  ),
  type: DioExceptionType.badResponse,
);

class PhotoApi extends ServiceRequestService {
  PhotoApi() : super(apiClient: ApiClient(tokenStorage: MemoryTokens()));
  final List<String> events = [];
  final Set<String> failures = {};
  final List<ServiceRequestAttachment> rows = [];
  Completer<ServiceRequestAttachment>? uploadPending;
  Completer<ServiceRequestModel>? createPending;
  Completer<List<ServiceRequestAttachment>>? listPending;
  bool failList = false;
  Object? uploadError;
  ServiceRequestStatus status = ServiceRequestStatus.created;
  @override
  Future<ServiceRequestModel> create(CreateServiceRequestDto dto) async {
    events.add('create');
    return createPending?.future ?? request();
  }

  @override
  Future<ServiceRequestModel> getById(String id) async {
    events.add('detail');
    return request(status: status);
  }

  @override
  Future<List<ServiceRequestModel>> getMyRequests() async => [
    request(status: status),
  ];
  @override
  Future<ServiceRequestAttachment> uploadAttachment(
    String id,
    ProblemPhoto photo, {
    CancelToken? cancelToken,
  }) async {
    events.add('upload:$id:${photo.file.name}');
    if (uploadError != null) throw uploadError!;
    if (failures.contains(photo.file.name)) throw photoFailure(400);
    final result = uploadPending != null
        ? await uploadPending!.future
        : attachment(rows.length + 1);
    rows.add(result);
    return result;
  }

  @override
  Future<List<ServiceRequestAttachment>> listAttachments(
    String id, {
    CancelToken? cancelToken,
  }) async {
    events.add('list');
    if (failList) throw photoFailure(503);
    return listPending?.future ?? List.of(rows);
  }

  @override
  Future<Uint8List> getAttachmentContent(
    String id,
    String attachmentId, {
    CancelToken? cancelToken,
  }) async {
    events.add('content:$attachmentId');
    return photoBytes;
  }

  @override
  Future<void> deleteAttachment(
    String id,
    String attachmentId, {
    CancelToken? cancelToken,
  }) async {
    events.add('delete:$attachmentId');
    rows.removeWhere((p) => p.id == attachmentId);
  }
}
