import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/service_requests/models/create_service_request_dto.dart';
import 'package:mobile/features/service_requests/models/problem_understanding_result_model.dart';
import 'package:mobile/features/service_requests/models/service_request_model.dart';
import 'package:mobile/features/service_requests/models/service_request_status.dart';
import 'package:mobile/features/service_requests/models/service_request_urgency.dart';
import 'package:mobile/features/service_requests/models/update_service_request_dto.dart';

void main() {
  group('ServiceRequestStatus', () {
    test('serializes and deserializes properly', () {
      expect(ServiceRequestStatus.created.toJson(), 'Created');
      expect(ServiceRequestStatus.analyzing.toJson(), 'Analyzing');
      expect(ServiceRequestStatus.awaitingInformation.toJson(), 'AwaitingInformation');
      expect(ServiceRequestStatus.analyzed.toJson(), 'Analyzed');
      expect(ServiceRequestStatus.readyForMatching.toJson(), 'ReadyForMatching');
      expect(ServiceRequestStatus.cancelled.toJson(), 'Cancelled');

      expect(ServiceRequestStatus.fromJson('Created'), ServiceRequestStatus.created);
      expect(ServiceRequestStatus.fromJson('AwaitingInformation'), ServiceRequestStatus.awaitingInformation);
      expect(ServiceRequestStatus.fromJson('readyformatching'), ServiceRequestStatus.readyForMatching);
      expect(ServiceRequestStatus.fromJson(1), ServiceRequestStatus.analyzing);
      expect(ServiceRequestStatus.fromJson('unknown_value'), ServiceRequestStatus.created);
    });

    test('displayName returns readable string', () {
      expect(ServiceRequestStatus.awaitingInformation.displayName, 'Awaiting Information');
      expect(ServiceRequestStatus.readyForMatching.displayName, 'Ready For Matching');
    });
  });

  group('ServiceRequestUrgency', () {
    test('serializes and deserializes properly', () {
      expect(ServiceRequestUrgency.unknown.toJson(), 'Unknown');
      expect(ServiceRequestUrgency.low.toJson(), 'Low');
      expect(ServiceRequestUrgency.medium.toJson(), 'Medium');
      expect(ServiceRequestUrgency.high.toJson(), 'High');
      expect(ServiceRequestUrgency.critical.toJson(), 'Critical');

      expect(ServiceRequestUrgency.fromJson('High'), ServiceRequestUrgency.high);
      expect(ServiceRequestUrgency.fromJson('critical'), ServiceRequestUrgency.critical);
      expect(ServiceRequestUrgency.fromJson(2), ServiceRequestUrgency.medium);
      expect(ServiceRequestUrgency.fromJson(null), ServiceRequestUrgency.unknown);
    });
  });

  group('ServiceRequestModel', () {
    test('parses json from backend response', () {
      final json = {
        'serviceRequestId': 'req-123',
        'customerId': 'cust-456',
        'category': 'Electrical',
        'description': 'Circuit breaker trips when turning on AC',
        'locationText': 'Colombo 03',
        'latitude': 6.9012,
        'longitude': 79.8529,
        'urgency': 'High',
        'status': 'Analyzed',
        'createdAt': '2026-09-09T10:00:00.000Z',
        'updatedAt': '2026-09-09T10:05:00.000Z',
      };

      final model = ServiceRequestModel.fromJson(json);

      expect(model.serviceRequestId, 'req-123');
      expect(model.customerId, 'cust-456');
      expect(model.category, 'Electrical');
      expect(model.description, 'Circuit breaker trips when turning on AC');
      expect(model.locationText, 'Colombo 03');
      expect(model.latitude, 6.9012);
      expect(model.longitude, 79.8529);
      expect(model.urgency, ServiceRequestUrgency.high);
      expect(model.status, ServiceRequestStatus.analyzed);
      expect(model.createdAt, DateTime.parse('2026-09-09T10:00:00.000Z'));
    });

    test('serializes to json', () {
      final model = ServiceRequestModel(
        serviceRequestId: 'req-1',
        customerId: 'cust-1',
        category: 'Plumbing',
        description: 'Leaking pipe under kitchen sink',
        locationText: 'Kandy',
        urgency: ServiceRequestUrgency.medium,
        status: ServiceRequestStatus.created,
        createdAt: DateTime.parse('2026-09-09T12:00:00.000Z'),
        updatedAt: DateTime.parse('2026-09-09T12:00:00.000Z'),
      );

      final json = model.toJson();
      expect(json['serviceRequestId'], 'req-1');
      expect(json['urgency'], 'Medium');
      expect(json['status'], 'Created');
    });
  });

  group('ProblemUnderstandingResultModel', () {
    test('parses backend ProblemUnderstandingResponseDto', () {
      final json = {
        'workflowId': 'wf-001',
        'executionId': 'ex-001',
        'serviceRequestId': 'req-123',
        'status': 'AwaitingInformation',
        'category': 'Air Conditioning',
        'problemSummary': 'AC outdoor unit is making humming noise without cooling',
        'urgency': 'Medium',
        'confidence': 0.88,
        'needsMoreInformation': true,
        'followUpQuestions': [
          'Is the outdoor fan spinning?',
          'What is the AC brand and capacity?'
        ],
      };

      final model = ProblemUnderstandingResultModel.fromJson(json);

      expect(model.workflowId, 'wf-001');
      expect(model.serviceRequestId, 'req-123');
      expect(model.status, ServiceRequestStatus.awaitingInformation);
      expect(model.category, 'Air Conditioning');
      expect(model.urgency, ServiceRequestUrgency.medium);
      expect(model.confidence, 0.88);
      expect(model.needsMoreInformation, true);
      expect(model.followUpQuestions.length, 2);
      expect(model.followUpQuestions[0], 'Is the outdoor fan spinning?');
    });
  });

  group('DTO Validation', () {
    test('CreateServiceRequestDto validates required fields', () {
      const invalid = CreateServiceRequestDto(description: '', locationText: '');
      expect(invalid.validate(), isNotNull);

      const valid = CreateServiceRequestDto(
        description: 'Fix electrical socket',
        locationText: 'Colombo',
      );
      expect(valid.validate(), isNull);
    });

    test('UpdateServiceRequestDto validates fields', () {
      const invalid = UpdateServiceRequestDto(description: '', locationText: '');
      expect(invalid.validate(), isNotNull);

      const valid = UpdateServiceRequestDto(
        description: 'Updated description',
        locationText: 'Updated Location',
      );
      expect(valid.validate(), isNull);
    });
  });
}
