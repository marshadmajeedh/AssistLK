import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/service_requests/models/canonical_service_category.dart';
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
      expect(model.categoryHint, isNull);
    });

    test('parses categoryHint from backend JSON', () {
      final json = {
        'serviceRequestId': 'req-123',
        'customerId': 'cust-456',
        'categoryHint': 'Plumbing',
        'category': 'Unclassified',
        'description': 'Water leaking from kitchen sink pipe',
        'locationText': 'Kandy',
        'urgency': 'Medium',
        'status': 'Created',
        'createdAt': '2026-09-09T10:00:00.000Z',
        'updatedAt': '2026-09-09T10:00:00.000Z',
      };

      final model = ServiceRequestModel.fromJson(json);
      expect(model.categoryHint, 'Plumbing');
      expect(model.category, 'Unclassified');
    });

    test('parses missing categoryHint as null (backward compatibility)', () {
      final json = {
        'serviceRequestId': 'req-123',
        'customerId': 'cust-456',
        'category': 'Plumbing',
        'description': 'Water leaking from kitchen sink pipe',
        'locationText': 'Kandy',
        'urgency': 'Medium',
        'status': 'Created',
        'createdAt': '2026-09-09T10:00:00.000Z',
        'updatedAt': '2026-09-09T10:00:00.000Z',
      };

      final model = ServiceRequestModel.fromJson(json);
      expect(model.categoryHint, isNull);
    });

    test('parses explicit null categoryHint safely', () {
      final json = {
        'serviceRequestId': 'req-123',
        'customerId': 'cust-456',
        'categoryHint': null,
        'category': 'Unclassified',
        'description': 'Some issue',
        'locationText': 'Colombo',
        'urgency': 'Low',
        'status': 'Created',
        'createdAt': '2026-09-09T10:00:00.000Z',
        'updatedAt': '2026-09-09T10:00:00.000Z',
      };

      final model = ServiceRequestModel.fromJson(json);
      expect(model.categoryHint, isNull);
    });

    test('serializes to json including categoryHint', () {
      final modelWithHint = ServiceRequestModel(
        serviceRequestId: 'req-1',
        customerId: 'cust-1',
        categoryHint: 'Vehicle Repair',
        category: 'Unclassified',
        description: 'Car engine won’t start',
        locationText: 'Galle',
        urgency: ServiceRequestUrgency.high,
        status: ServiceRequestStatus.created,
        createdAt: DateTime.parse('2026-09-09T12:00:00.000Z'),
        updatedAt: DateTime.parse('2026-09-09T12:00:00.000Z'),
      );

      final jsonWithHint = modelWithHint.toJson();
      expect(jsonWithHint['serviceRequestId'], 'req-1');
      expect(jsonWithHint['categoryHint'], 'Vehicle Repair');
      expect(jsonWithHint['category'], 'Unclassified');

      final modelWithoutHint = ServiceRequestModel(
        serviceRequestId: 'req-2',
        customerId: 'cust-2',
        category: 'Electrical',
        description: 'Sparking outlet',
        locationText: 'Colombo',
        urgency: ServiceRequestUrgency.critical,
        status: ServiceRequestStatus.created,
        createdAt: DateTime.parse('2026-09-09T12:00:00.000Z'),
        updatedAt: DateTime.parse('2026-09-09T12:00:00.000Z'),
      );

      final jsonWithoutHint = modelWithoutHint.toJson();
      expect(jsonWithoutHint.containsKey('categoryHint'), isTrue);
      expect(jsonWithoutHint['categoryHint'], isNull);
    });

    test('copyWith preserves categoryHint when omitted', () {
      final original = ServiceRequestModel(
        serviceRequestId: 'req-1',
        customerId: 'cust-1',
        categoryHint: 'Plumbing',
        category: 'Unclassified',
        description: 'Leaking pipe',
        locationText: 'Kandy',
        urgency: ServiceRequestUrgency.medium,
        status: ServiceRequestStatus.created,
        createdAt: DateTime.parse('2026-09-09T12:00:00.000Z'),
        updatedAt: DateTime.parse('2026-09-09T12:00:00.000Z'),
      );

      final copy = original.copyWith(status: ServiceRequestStatus.analyzing);
      expect(copy.categoryHint, 'Plumbing');
      expect(copy.status, ServiceRequestStatus.analyzing);
    });

    test('copyWith can replace categoryHint with a new value', () {
      final original = ServiceRequestModel(
        serviceRequestId: 'req-1',
        customerId: 'cust-1',
        categoryHint: 'Plumbing',
        category: 'Unclassified',
        description: 'Leaking pipe',
        locationText: 'Kandy',
        urgency: ServiceRequestUrgency.medium,
        status: ServiceRequestStatus.created,
        createdAt: DateTime.parse('2026-09-09T12:00:00.000Z'),
        updatedAt: DateTime.parse('2026-09-09T12:00:00.000Z'),
      );

      final copy = original.copyWith(categoryHint: 'Electrical');
      expect(copy.categoryHint, 'Electrical');
    });

    test('copyWith can replace categoryHint with null', () {
      final original = ServiceRequestModel(
        serviceRequestId: 'req-1',
        customerId: 'cust-1',
        categoryHint: 'Plumbing',
        category: 'Unclassified',
        description: 'Leaking pipe',
        locationText: 'Kandy',
        urgency: ServiceRequestUrgency.medium,
        status: ServiceRequestStatus.created,
        createdAt: DateTime.parse('2026-09-09T12:00:00.000Z'),
        updatedAt: DateTime.parse('2026-09-09T12:00:00.000Z'),
      );

      final copy = original.copyWith(categoryHint: null);
      expect(copy.categoryHint, isNull);
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

  group('DTO Validation and Serialization', () {
    test('CreateServiceRequestDto validates required fields', () {
      const invalid = CreateServiceRequestDto(description: '', locationText: '');
      expect(invalid.validate(), isNotNull);

      const valid = CreateServiceRequestDto(
        description: 'Fix electrical socket',
        locationText: 'Colombo',
      );
      expect(valid.validate(), isNull);
    });

    test('CreateServiceRequestDto accepts null categoryHint', () {
      const dto = CreateServiceRequestDto(
        description: 'Fix electrical socket',
        locationText: 'Colombo',
        categoryHint: null,
      );
      expect(dto.categoryHint, isNull);
      expect(dto.validate(), isNull);
    });

    test('CreateServiceRequestDto serializes Plumbing', () {
      const dto = CreateServiceRequestDto(
        description: 'Fix leaky tap',
        locationText: 'Colombo',
        categoryHint: 'Plumbing',
      );
      final json = dto.toJson();
      expect(json['categoryHint'], 'Plumbing');
    });

    test('CreateServiceRequestDto serializes Electrical', () {
      const dto = CreateServiceRequestDto(
        description: 'Tripping circuit breaker',
        locationText: 'Colombo',
        categoryHint: 'Electrical',
      );
      final json = dto.toJson();
      expect(json['categoryHint'], 'Electrical');
    });

    test('CreateServiceRequestDto serializes Vehicle Repair', () {
      const dto = CreateServiceRequestDto(
        description: 'Flat tyre replacement',
        locationText: 'Colombo',
        categoryHint: 'Vehicle Repair',
      );
      final json = dto.toJson();
      expect(json['categoryHint'], 'Vehicle Repair');
    });

    test('CreateServiceRequestDto serializes Appliance Repair', () {
      const dto = CreateServiceRequestDto(
        description: 'Fridge compressor not running',
        locationText: 'Colombo',
        categoryHint: 'Appliance Repair',
      );
      final json = dto.toJson();
      expect(json['categoryHint'], 'Appliance Repair');
    });

    test('CreateServiceRequestDto handles null categoryHint correctly (omitted in toJson)', () {
      const dto = CreateServiceRequestDto(
        description: 'General problem description',
        locationText: 'Colombo',
      );
      final json = dto.toJson();
      expect(json.containsKey('categoryHint'), isFalse);
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

    test('UpdateServiceRequestDto serializes a valid hint', () {
      const dto = UpdateServiceRequestDto(
        description: 'Updated description',
        locationText: 'Updated Location',
        categoryHint: 'Plumbing',
      );
      final json = dto.toJson();
      expect(json['categoryHint'], 'Plumbing');
    });

    test('UpdateServiceRequestDto explicitly transmits null to clear an existing hint', () {
      const dto = UpdateServiceRequestDto(
        description: 'Updated description',
        locationText: 'Updated Location',
        categoryHint: null,
      );
      final json = dto.toJson();
      expect(json.containsKey('categoryHint'), isTrue);
      expect(json['categoryHint'], isNull);
    });
  });

  group('CanonicalServiceCategory Mapping Safety', () {
    test('"Let AI identify" maps to null', () {
      expect(CanonicalServiceCategory.toCanonicalCategoryHint('Let AI identify'), isNull);
      expect(CanonicalServiceCategory.toCanonicalCategoryHint('let ai identify'), isNull);
      expect(CanonicalServiceCategory.toCanonicalCategoryHint('LET AI IDENTIFY'), isNull);
    });

    test('null and empty/whitespace map to null', () {
      expect(CanonicalServiceCategory.toCanonicalCategoryHint(null), isNull);
      expect(CanonicalServiceCategory.toCanonicalCategoryHint(''), isNull);
      expect(CanonicalServiceCategory.toCanonicalCategoryHint('   '), isNull);
    });

    test('Vehicle Assistance maps to Vehicle Repair', () {
      expect(
        CanonicalServiceCategory.toCanonicalCategoryHint('Vehicle Assistance'),
        'Vehicle Repair',
      );
      expect(
        CanonicalServiceCategory.toCanonicalCategoryHint('vehicle assistance'),
        'Vehicle Repair',
      );
    });

    test('canonical categories map to themselves', () {
      expect(CanonicalServiceCategory.toCanonicalCategoryHint('Plumbing'), 'Plumbing');
      expect(CanonicalServiceCategory.toCanonicalCategoryHint('Electrical'), 'Electrical');
      expect(CanonicalServiceCategory.toCanonicalCategoryHint('Vehicle Repair'), 'Vehicle Repair');
      expect(CanonicalServiceCategory.toCanonicalCategoryHint('Appliance Repair'), 'Appliance Repair');
    });

    test('Unclassified is rejected and throws ArgumentError', () {
      expect(
        () => CanonicalServiceCategory.toCanonicalCategoryHint('Unclassified'),
        throwsArgumentError,
      );
    });

    test('Cleaning is rejected and throws ArgumentError', () {
      expect(
        () => CanonicalServiceCategory.toCanonicalCategoryHint('Cleaning'),
        throwsArgumentError,
      );
    });

    test('arbitrary values are rejected and throw ArgumentError', () {
      expect(
        () => CanonicalServiceCategory.toCanonicalCategoryHint('AC Service'),
        throwsArgumentError,
      );
      expect(
        () => CanonicalServiceCategory.toCanonicalCategoryHint('Vehicle'),
        throwsArgumentError,
      );
      expect(
        () => CanonicalServiceCategory.toCanonicalCategoryHint('Emergency'),
        throwsArgumentError,
      );
      expect(
        () => CanonicalServiceCategory.toCanonicalCategoryHint('random string'),
        throwsArgumentError,
      );
    });
  });
}
