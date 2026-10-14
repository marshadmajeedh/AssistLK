import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/service_requests/models/canonical_service_category.dart';
import 'package:mobile/features/service_requests/models/create_service_request_dto.dart';
import 'package:mobile/features/service_requests/models/problem_analysis_summary_model.dart';
import 'package:mobile/features/service_requests/models/problem_understanding_result_model.dart';
import 'package:mobile/features/service_requests/models/service_request_clarification_model.dart';
import 'package:mobile/features/service_requests/models/service_request_model.dart';
import 'package:mobile/features/service_requests/models/service_request_status.dart';
import 'package:mobile/features/service_requests/models/service_request_urgency.dart';
import 'package:mobile/features/service_requests/models/update_service_request_dto.dart';

/**
 * TEST SUITE: Mobile Service Request Domain Models and DTOs
 *
 * 1. What this file tests:
 *    - Verifies serialization, deserialization, backward compatibility, and business logic
 *      for all Dart domain models, enums, DTOs, and category mapping functions supporting
 *      Component 1 (Service Request and Problem Understanding) in the Flutter mobile application.
 * 2. Why these tests are needed:
 *    - Guarantees seamless JSON data exchange with the ASP.NET Core backend REST APIs.
 *    - Validates category hint propagation, multi-round clarification lifecycle tracking,
 *      geographic coordinate bounding (-90..90 lat, -180..180 lng), and matching expiry calculations.
 * 3. What is real and what is mocked/faked:
 *    - Real: Pure Dart models, JSON map transformations, enum string parsers, and validation logic.
 *    - Mocked: None; fast offline unit tests executing entirely in memory without device or network dependencies.
 * 4. Prerequisites:
 *    - Flutter SDK installed and packages resolved (`flutter pub get` inside `/mobile`).
 * 5. PowerShell command to run this file from repository root:
 *    Push-Location mobile; flutter test test/service_request_models_test.dart; Pop-Location
 */

void main() {
  /**
   * TEST GROUP: ServiceRequestStatus Enum Serialization and Parsing
   *
   * 1. What this group tests:
   *    - Verifies JSON string serialization, case-insensitive string deserialization,
   *      integer fallback parsing, unknown value fallback to `created`, and user-friendly display names.
   * 2. Why these tests are needed:
   *    - Backend status strings drive client screen rendering and action button availability.
   * 3. What is real and what is mocked:
   *    - Real Dart enum logic.
   * 4. Prerequisites: None.
   * 5. PowerShell command to run this group from repository root:
   *    Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "ServiceRequestStatus"; Pop-Location
   */
  group('ServiceRequestStatus', () {
    /**
     * Purpose: What this specific test checks
     * - Verifies serialization to backend strings ('Created', 'Analyzing', etc.) and
     *   robust deserialization handling varied casing, integer IDs, and invalid fallbacks.
     *
     * Why: Why this scenario matters
     * - Protects against app crashes if backend sends numbers or unknown status strings.
     *
     * Scenario: Input or condition being tested
     * - Convert enums to JSON, and parse strings 'Created', 'readyformatching', integer 1, and 'unknown_value'.
     *
     * Pass condition: What result or assertion makes it pass
     * - Each enum matches its expected string representation and parses to the right enum value.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "ServiceRequestStatus serializes and deserializes properly"; Pop-Location
     */
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

    /**
     * Purpose: What this specific test checks
     * - Verifies that the `displayName` getter returns spaced, capitalized English labels for UI presentation.
     *
     * Why: Why this scenario matters
     * - Prevents raw PascalCase strings like "AwaitingInformation" from showing in customer UI.
     *
     * Scenario: Input or condition being tested
     * - Call `.displayName` on `awaitingInformation` and `readyForMatching`.
     *
     * Pass condition: What result or assertion makes it pass
     * - Returns 'Awaiting Information' and 'Ready For Matching'.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "ServiceRequestStatus displayName returns readable string"; Pop-Location
     */
    test('displayName returns readable string', () {
      expect(ServiceRequestStatus.awaitingInformation.displayName, 'Awaiting Information');
      expect(ServiceRequestStatus.readyForMatching.displayName, 'Ready For Matching');
    });
  });

  /**
   * TEST GROUP: ServiceRequestUrgency Enum Serialization and Parsing
   *
   * 1. What this group tests:
   *    - Verifies JSON roundtrip serialization and deserialization for urgency levels (Low, Medium, High, Critical, Unknown).
   * 2. Why these tests are needed:
   *    - Urgency levels determine priority sorting, dispatch SLA alerts, and visual badge colors.
   * 3. What is real and what is mocked:
   *    - Real Dart enum logic.
   * 4. Prerequisites: None.
   * 5. PowerShell command to run this group from repository root:
   *    Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "ServiceRequestUrgency"; Pop-Location
   */
  group('ServiceRequestUrgency', () {
    /**
     * Purpose: What this specific test checks
     * - Verifies serialization to JSON and parsing from strings, lowercase strings, integers, and nulls.
     *
     * Why: Why this scenario matters
     * - Null urgency values from unanalyzed requests must safely fallback to `unknown` without throwing exceptions.
     *
     * Scenario: Input or condition being tested
     * - Convert urgency enums to strings and parse 'High', 'critical', 2, and null.
     *
     * Pass condition: What result or assertion makes it pass
     * - Enums map to proper string values and parse back accurately.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "ServiceRequestUrgency serializes and deserializes properly"; Pop-Location
     */
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

  /**
   * TEST GROUP: ServiceRequestModel Parsing, Category Hints, and Copying
   *
   * 1. What this group tests:
   *    - Verifies full JSON deserialization of backend service request records, parsing of optional
   *      customer category hints, backward compatibility for legacy records lacking category hints,
   *      and immutable `copyWith` property transformations.
   * 2. Why these tests are needed:
   *    - `ServiceRequestModel` is the primary domain entity representing customer requests in the mobile app.
   * 3. What is real and what is mocked:
   *    - Real model instantiations and JSON parsing.
   * 4. Prerequisites: None.
   * 5. PowerShell command to run this group from repository root:
   *    Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "ServiceRequestModel"; Pop-Location
   */
  group('ServiceRequestModel', () {
    /**
     * Purpose: What this specific test checks
     * - Verifies that all standard fields (ID, customerId, category, description, location, coordinates, urgency, status, dates)
     *   are populated accurately from a full backend JSON payload.
     *
     * Why: Why this scenario matters
     * - Validates field-by-field contract alignment between backend DTO and mobile model.
     *
     * Scenario: Input or condition being tested
     * - Parse standard JSON map containing request fields and coordinates.
     *
     * Pass condition: What result or assertion makes it pass
     * - Model fields match parsed JSON values and categoryHint is null.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "ServiceRequestModel parses json from backend response"; Pop-Location
     */
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

    /**
     * Purpose: What this specific test checks
     * - Verifies that `categoryHint` is extracted from backend JSON while preserving the authoritative `category`.
     *
     * Why: Why this scenario matters
     * - Before AI analysis completes, the customer's selected hint guides provisional display.
     *
     * Scenario: Input or condition being tested
     * - JSON with `categoryHint: 'Plumbing'` and `category: 'Unclassified'`.
     *
     * Pass condition: What result or assertion makes it pass
     * - `model.categoryHint` is 'Plumbing' and `model.category` is 'Unclassified'.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "ServiceRequestModel parses categoryHint from backend JSON"; Pop-Location
     */
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

    /**
     * Purpose: What this specific test checks
     * - Verifies backward compatibility when `categoryHint` key is missing entirely from JSON.
     *
     * Why: Why this scenario matters
     * - Existing records created before category hints were introduced must not throw missing-key errors.
     *
     * Scenario: Input or condition being tested
     * - Legacy JSON payload with no `categoryHint` key.
     *
     * Pass condition: What result or assertion makes it pass
     * - `model.categoryHint` defaults cleanly to null.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "ServiceRequestModel parses missing categoryHint as null"; Pop-Location
     */
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

    /**
     * Purpose: What this specific test checks
     * - Verifies that an explicit JSON `null` for `categoryHint` is handled safely without crashing.
     *
     * Why: Why this scenario matters
     * - Requests created with "Let AI identify" explicitly transmit `null` for categoryHint.
     *
     * Scenario: Input or condition being tested
     * - JSON with `'categoryHint': null`.
     *
     * Pass condition: What result or assertion makes it pass
     * - `model.categoryHint` is null.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "ServiceRequestModel parses explicit null categoryHint safely"; Pop-Location
     */
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

    /**
     * Purpose: What this specific test checks
     * - Verifies that `toJson()` serializes `categoryHint` when present, and emits null when absent.
     *
     * Why: Why this scenario matters
     * - Ensures client-side offline caches or state serializers preserve the category hint attribute.
     *
     * Scenario: Input or condition being tested
     * - Serialize a model with `categoryHint: 'Vehicle Repair'` and another with `categoryHint: null`.
     *
     * Pass condition: What result or assertion makes it pass
     * - Serialized JSON map contains 'categoryHint' matching the model's value.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "ServiceRequestModel serializes to json including categoryHint"; Pop-Location
     */
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

    /**
     * Purpose: What this specific test checks
     * - Verifies that `copyWith` maintains the original `categoryHint` when the parameter is omitted.
     *
     * Why: Why this scenario matters
     * - Updating other fields (e.g. status transition to Analyzing) must not accidentally drop the category hint.
     *
     * Scenario: Input or condition being tested
     * - Call `original.copyWith(status: ServiceRequestStatus.analyzing)`.
     *
     * Pass condition: What result or assertion makes it pass
     * - Copy preserves `categoryHint: 'Plumbing'` and updates status.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "ServiceRequestModel copyWith preserves categoryHint when omitted"; Pop-Location
     */
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

    /**
     * Purpose: What this specific test checks
     * - Verifies that `copyWith` can update `categoryHint` to a new category string.
     *
     * Why: Why this scenario matters
     * - Customer editing their request can switch hints (e.g. Plumbing -> Electrical).
     *
     * Scenario: Input or condition being tested
     * - Call `original.copyWith(categoryHint: 'Electrical')`.
     *
     * Pass condition: What result or assertion makes it pass
     * - Copy has `categoryHint: 'Electrical'`.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "ServiceRequestModel copyWith can replace categoryHint with a new value"; Pop-Location
     */
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

    /**
     * Purpose: What this specific test checks
     * - Verifies that `copyWith` can explicitly clear an existing hint to null.
     *
     * Why: Why this scenario matters
     * - Customer editing a request can clear their prior hint by selecting "Let AI identify".
     *
     * Scenario: Input or condition being tested
     * - Call `original.copyWith(categoryHint: null)`.
     *
     * Pass condition: What result or assertion makes it pass
     * - Copy has `categoryHint: isNull`.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "ServiceRequestModel copyWith can replace categoryHint with null"; Pop-Location
     */
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

  /**
   * TEST GROUP: ProblemUnderstandingResultModel Parsing
   *
   * 1. What this group tests:
   *    - Verifies JSON parsing of the AI diagnostic result model (`ProblemUnderstandingResultModel`),
   *      including workflow IDs, execution IDs, detected category, problem summary, confidence score,
   *      needsMoreInformation flag, and follow-up question lists.
   * 2. Why these tests are needed:
   *    - Represents the AI agent's structured response returned via backend API to the mobile client.
   * 3. What is real and what is mocked:
   *    - Real Dart model deserialization.
   * 4. Prerequisites: None.
   * 5. PowerShell command to run this group from repository root:
   *    Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "ProblemUnderstandingResultModel"; Pop-Location
   */
  group('ProblemUnderstandingResultModel', () {
    /**
     * Purpose: What this specific test checks
     * - Verifies parsing of backend `ProblemUnderstandingResponseDto` fields into Dart properties.
     *
     * Why: Why this scenario matters
     * - Mobile screens display AI diagnostic confidence, problem summaries, and clarification questions from this model.
     *
     * Scenario: Input or condition being tested
     * - Parse JSON with status "AwaitingInformation", category "Air Conditioning", confidence 0.88, and 2 questions.
     *
     * Pass condition: What result or assertion makes it pass
     * - All model fields match the JSON values accurately.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "ProblemUnderstandingResultModel parses backend ProblemUnderstandingResponseDto"; Pop-Location
     */
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

  /**
   * TEST GROUP: DTO Validation and Serialization
   *
   * 1. What this group tests:
   *    - Verifies input validation and JSON serialization for `CreateServiceRequestDto` and `UpdateServiceRequestDto`.
   *    - Covers required field validation (description and locationText non-empty), category hint propagation
   *      and omission, and strict coordinate pair validation (latitude between -90 and 90, longitude between -180 and 180,
   *      and mutual pair requirement).
   * 2. Why these tests are needed:
   *    - Validates client-side validation logic before sending HTTP requests to the backend API,
   *      preventing unnecessary 400 Bad Request network roundtrips.
   * 3. What is real and what is mocked:
   *    - Real Dart DTO validation and serialization.
   * 4. Prerequisites: None.
   * 5. PowerShell command to run this group from repository root:
   *    Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "DTO Validation and Serialization"; Pop-Location
   */
  group('DTO Validation and Serialization', () {
    /**
     * Purpose: What this specific test checks
     * - Verifies that `CreateServiceRequestDto.validate()` rejects empty strings and passes valid descriptions/locations.
     *
     * Why: Why this scenario matters
     * - Both problem description and location text are mandatory for service dispatch.
     *
     * Scenario: Input or condition being tested
     * - Validate DTO with empty strings `''`, then with non-empty strings.
     *
     * Pass condition: What result or assertion makes it pass
     * - Empty strings return validation error message (`isNotNull`); valid strings return `isNull`.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "DTO Validation and Serialization CreateServiceRequestDto validates required fields"; Pop-Location
     */
    test('CreateServiceRequestDto validates required fields', () {
      const invalid = CreateServiceRequestDto(description: '', locationText: '');
      expect(invalid.validate(), isNotNull);

      const valid = CreateServiceRequestDto(
        description: 'Fix electrical socket',
        locationText: 'Colombo',
      );
      expect(valid.validate(), isNull);
    });

    /**
     * Purpose: What this specific test checks
     * - Verifies that `CreateServiceRequestDto` accepts `categoryHint: null` as valid.
     *
     * Why: Why this scenario matters
     * - Customer category hints are strictly optional; choosing "Let AI identify" passes null.
     *
     * Scenario: Input or condition being tested
     * - Create DTO with `categoryHint: null`.
     *
     * Pass condition: What result or assertion makes it pass
     * - `dto.categoryHint` is null and `dto.validate()` returns null.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "DTO Validation and Serialization CreateServiceRequestDto accepts null categoryHint"; Pop-Location
     */
    test('CreateServiceRequestDto accepts null categoryHint', () {
      const dto = CreateServiceRequestDto(
        description: 'Fix electrical socket',
        locationText: 'Colombo',
        categoryHint: null,
      );
      expect(dto.categoryHint, isNull);
      expect(dto.validate(), isNull);
    });

    /**
     * Purpose: What this specific test checks
     * - Verifies serialization of "Plumbing" category hint in create payload.
     *
     * Why: Why this scenario matters
     * - Backend expects exact canonical category string casing.
     *
     * Scenario: Input or condition being tested
     * - DTO with `categoryHint: 'Plumbing'` converted to JSON.
     *
     * Pass condition: What result or assertion makes it pass
     * - `json['categoryHint']` equals 'Plumbing'.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "DTO Validation and Serialization CreateServiceRequestDto serializes Plumbing"; Pop-Location
     */
    test('CreateServiceRequestDto serializes Plumbing', () {
      const dto = CreateServiceRequestDto(
        description: 'Fix leaky tap',
        locationText: 'Colombo',
        categoryHint: 'Plumbing',
      );
      final json = dto.toJson();
      expect(json['categoryHint'], 'Plumbing');
    });

    /**
     * Purpose: What this specific test checks
     * - Verifies serialization of "Electrical" category hint in create payload.
     *
     * Why: Why this scenario matters
     * - Validates canonical category hint transmission.
     *
     * Scenario: Input or condition being tested
     * - DTO with `categoryHint: 'Electrical'` converted to JSON.
     *
     * Pass condition: What result or assertion makes it pass
     * - `json['categoryHint']` equals 'Electrical'.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "DTO Validation and Serialization CreateServiceRequestDto serializes Electrical"; Pop-Location
     */
    test('CreateServiceRequestDto serializes Electrical', () {
      const dto = CreateServiceRequestDto(
        description: 'Tripping circuit breaker',
        locationText: 'Colombo',
        categoryHint: 'Electrical',
      );
      final json = dto.toJson();
      expect(json['categoryHint'], 'Electrical');
    });

    /**
     * Purpose: What this specific test checks
     * - Verifies serialization of "Vehicle Repair" category hint in create payload.
     *
     * Why: Why this scenario matters
     * - Validates multi-word category string transmission.
     *
     * Scenario: Input or condition being tested
     * - DTO with `categoryHint: 'Vehicle Repair'` converted to JSON.
     *
     * Pass condition: What result or assertion makes it pass
     * - `json['categoryHint']` equals 'Vehicle Repair'.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "DTO Validation and Serialization CreateServiceRequestDto serializes Vehicle Repair"; Pop-Location
     */
    test('CreateServiceRequestDto serializes Vehicle Repair', () {
      const dto = CreateServiceRequestDto(
        description: 'Flat tyre replacement',
        locationText: 'Colombo',
        categoryHint: 'Vehicle Repair',
      );
      final json = dto.toJson();
      expect(json['categoryHint'], 'Vehicle Repair');
    });

    /**
     * Purpose: What this specific test checks
     * - Verifies serialization of "Appliance Repair" category hint in create payload.
     *
     * Why: Why this scenario matters
     * - Validates appliance repair category string transmission.
     *
     * Scenario: Input or condition being tested
     * - DTO with `categoryHint: 'Appliance Repair'` converted to JSON.
     *
     * Pass condition: What result or assertion makes it pass
     * - `json['categoryHint']` equals 'Appliance Repair'.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "DTO Validation and Serialization CreateServiceRequestDto serializes Appliance Repair"; Pop-Location
     */
    test('CreateServiceRequestDto serializes Appliance Repair', () {
      const dto = CreateServiceRequestDto(
        description: 'Fridge compressor not running',
        locationText: 'Colombo',
        categoryHint: 'Appliance Repair',
      );
      final json = dto.toJson();
      expect(json['categoryHint'], 'Appliance Repair');
    });

    /**
     * Purpose: What this specific test checks
     * - Verifies that when `categoryHint` is null, the key is omitted entirely from `toJson()` output.
     *
     * Why: Why this scenario matters
     * - Backend ASP.NET model binder treats omitted keys cleanly without deserialization ambiguities.
     *
     * Scenario: Input or condition being tested
     * - Convert DTO with no category hint to JSON.
     *
     * Pass condition: What result or assertion makes it pass
     * - `json.containsKey('categoryHint')` is false.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "DTO Validation and Serialization CreateServiceRequestDto handles null categoryHint correctly"; Pop-Location
     */
    test('CreateServiceRequestDto handles null categoryHint correctly (omitted in toJson)', () {
      const dto = CreateServiceRequestDto(
        description: 'General problem description',
        locationText: 'Colombo',
      );
      final json = dto.toJson();
      expect(json.containsKey('categoryHint'), isFalse);
    });

    /**
     * Purpose: What this specific test checks
     * - Verifies that `UpdateServiceRequestDto.validate()` enforces non-empty description and location text.
     *
     * Why: Why this scenario matters
     * - Request updates cannot wipe out description or address.
     *
     * Scenario: Input or condition being tested
     * - Validate invalid update DTO with empty strings, then valid DTO.
     *
     * Pass condition: What result or assertion makes it pass
     * - Invalid returns error; valid returns null.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "DTO Validation and Serialization UpdateServiceRequestDto validates fields"; Pop-Location
     */
    test('UpdateServiceRequestDto validates fields', () {
      const invalid = UpdateServiceRequestDto(description: '', locationText: '');
      expect(invalid.validate(), isNotNull);

      const valid = UpdateServiceRequestDto(
        description: 'Updated description',
        locationText: 'Updated Location',
      );
      expect(valid.validate(), isNull);
    });

    /**
     * Purpose: What this specific test checks
     * - Verifies that `UpdateServiceRequestDto` serializes valid category hints.
     *
     * Why: Why this scenario matters
     * - Supports editing category hints on existing draft requests.
     *
     * Scenario: Input or condition being tested
     * - DTO with `categoryHint: 'Plumbing'` converted to JSON.
     *
     * Pass condition: What result or assertion makes it pass
     * - `json['categoryHint']` is 'Plumbing'.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "DTO Validation and Serialization UpdateServiceRequestDto serializes a valid hint"; Pop-Location
     */
    test('UpdateServiceRequestDto serializes a valid hint', () {
      const dto = UpdateServiceRequestDto(
        description: 'Updated description',
        locationText: 'Updated Location',
        categoryHint: 'Plumbing',
      );
      final json = dto.toJson();
      expect(json['categoryHint'], 'Plumbing');
    });

    /**
     * Purpose: What this specific test checks
     * - Verifies that `UpdateServiceRequestDto` explicitly includes `'categoryHint': null` in its JSON output.
     *
     * Why: Why this scenario matters
     * - Distinct from create: an update MUST transmit explicit null to tell the backend database to clear an existing hint.
     *
     * Scenario: Input or condition being tested
     * - Update DTO with `categoryHint: null` converted to JSON.
     *
     * Pass condition: What result or assertion makes it pass
     * - `json.containsKey('categoryHint')` is true and its value is null.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "DTO Validation and Serialization UpdateServiceRequestDto explicitly transmits null to clear an existing hint"; Pop-Location
     */
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

    /**
     * Purpose: What this specific test checks
     * - Verifies comprehensive coordinate validation: valid coordinate pair serializes, omitted coordinates omit keys,
     *   half-provided coordinate (latitude without longitude or vice versa) returns error,
     *   and out-of-range coordinates (> 90 lat or > 180 lng) return specific validation errors.
     *
     * Why: Why this scenario matters
     * - Prevents invalid geographic data from polluting the database or failing PostgreSQL spatial constraints.
     *
     * Scenario: Input or condition being tested
     * - Test valid pair (6.9271, 79.8612), no coords, onlyLat, onlyLng, lat 95.0, lng 195.0.
     *
     * Pass condition: What result or assertion makes it pass
     * - Validation catches coordinate mismatches with exact error messages and serializes valid pairs.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "DTO Validation and Serialization CreateServiceRequestDto validates and serializes coordinate pair"; Pop-Location
     */
    test('CreateServiceRequestDto validates and serializes coordinate pair', () {
      const withCoords = CreateServiceRequestDto(
        description: 'Leaking pipe under sink',
        locationText: 'Colombo 03',
        latitude: 6.9271,
        longitude: 79.8612,
      );
      expect(withCoords.validate(), isNull);
      final json = withCoords.toJson();
      expect(json['latitude'], 6.9271);
      expect(json['longitude'], 79.8612);

      const noCoords = CreateServiceRequestDto(
        description: 'Leaking pipe under sink',
        locationText: 'Colombo 03',
      );
      expect(noCoords.validate(), isNull);
      final noJson = noCoords.toJson();
      expect(noJson.containsKey('latitude'), isFalse);
      expect(noJson.containsKey('longitude'), isFalse);

      const onlyLat = CreateServiceRequestDto(
        description: 'Leaking pipe under sink',
        locationText: 'Colombo 03',
        latitude: 6.9271,
      );
      expect(onlyLat.validate(), 'Both latitude and longitude must be provided together.');

      const onlyLng = CreateServiceRequestDto(
        description: 'Leaking pipe under sink',
        locationText: 'Colombo 03',
        longitude: 79.8612,
      );
      expect(onlyLng.validate(), 'Both latitude and longitude must be provided together.');

      const invalidLat = CreateServiceRequestDto(
        description: 'Leaking pipe under sink',
        locationText: 'Colombo 03',
        latitude: 95.0,
        longitude: 79.8612,
      );
      expect(invalidLat.validate(), 'Latitude must be between -90 and 90.');

      const invalidLng = CreateServiceRequestDto(
        description: 'Leaking pipe under sink',
        locationText: 'Colombo 03',
        latitude: 6.9271,
        longitude: 195.0,
      );
      expect(invalidLng.validate(), 'Longitude must be between -180 and 180.');
    });

    /**
     * Purpose: What this specific test checks
     * - Verifies that `UpdateServiceRequestDto` validates coordinate pairs, allows explicitly clearing
     *   coordinates with nulls, and rejects unpaired latitude.
     *
     * Why: Why this scenario matters
     * - Customer editing a request can update coordinates, remove coordinates, or fail validation on partial edits.
     *
     * Scenario: Input or condition being tested
     * - Test valid update coords, clearing coords (null, null), and unpaired latitude.
     *
     * Pass condition: What result or assertion makes it pass
     * - Serialization contains coordinates, clear coordinates set nulls, unpaired latitude throws validation error.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "DTO Validation and Serialization UpdateServiceRequestDto validates and serializes coordinates"; Pop-Location
     */
    test('UpdateServiceRequestDto validates and serializes coordinates', () {
      const withCoords = UpdateServiceRequestDto(
        description: 'Updated pipe leak',
        locationText: 'Colombo 03',
        latitude: 6.9271,
        longitude: 79.8612,
      );
      expect(withCoords.validate(), isNull);
      final json = withCoords.toJson();
      expect(json['latitude'], 6.9271);
      expect(json['longitude'], 79.8612);

      const clearCoords = UpdateServiceRequestDto(
        description: 'Updated pipe leak',
        locationText: 'Colombo 03',
        latitude: null,
        longitude: null,
      );
      expect(clearCoords.validate(), isNull);
      final clearJson = clearCoords.toJson();
      expect(clearJson['latitude'], isNull);
      expect(clearJson['longitude'], isNull);

      const onlyLat = UpdateServiceRequestDto(
        description: 'Updated pipe leak',
        locationText: 'Colombo 03',
        latitude: 6.9271,
      );
      expect(onlyLat.validate(), 'Both latitude and longitude must be provided together.');
    });
  });

  /**
   * TEST GROUP: CanonicalServiceCategory Mapping Safety
   *
   * 1. What this group tests:
   *    - Verifies category sanitization and mapping logic in `CanonicalServiceCategory.toCanonicalCategoryHint`.
   *    - Confirms that "Let AI identify" (case-insensitive) and empty/whitespace/null strings map safely to `null`.
   *    - Confirms synonym/legacy category mapping (e.g., 'Vehicle Assistance' -> 'Vehicle Repair').
   *    - Confirms canonical categories ('Plumbing', 'Electrical', 'Vehicle Repair', 'Appliance Repair') pass through unchanged.
   *    - Confirms that non-canonical categories ('Unclassified', 'Cleaning') and arbitrary strings throw `ArgumentError`.
   * 2. Why these tests are needed:
   *    - Guarantees data integrity for the category hint passed to the backend API and Problem Understanding Agent,
   *      preventing invalid or obsolete category labels from poisoning downstream classification and dispatch.
   * 3. What is real and what is mocked:
   *    - Real Dart pure function evaluation (`CanonicalServiceCategory.toCanonicalCategoryHint`). No mocks.
   * 4. Prerequisites: None.
   * 5. PowerShell command to run this group from repository root:
   *    Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "CanonicalServiceCategory Mapping Safety"; Pop-Location
   */
  group('CanonicalServiceCategory Mapping Safety', () {
    /**
     * Purpose: What this specific test checks
     * - Verifies that "Let AI identify" in any casing (mixed, lower, upper) maps to `null`.
     *
     * Why: Why this scenario matters
     * - When customers choose "Let AI identify", no category hint should be forced on the AI agent.
     *
     * Scenario: Input or condition being tested
     * - Inputs `'Let AI identify'`, `'let ai identify'`, `'LET AI IDENTIFY'`.
     *
     * Pass condition: What result or assertion makes it pass
     * - All evaluate to `isNull`.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "CanonicalServiceCategory Mapping Safety \"Let AI identify\" maps to null"; Pop-Location
     */
    test('"Let AI identify" maps to null', () {
      expect(CanonicalServiceCategory.toCanonicalCategoryHint('Let AI identify'), isNull);
      expect(CanonicalServiceCategory.toCanonicalCategoryHint('let ai identify'), isNull);
      expect(CanonicalServiceCategory.toCanonicalCategoryHint('LET AI IDENTIFY'), isNull);
    });

    /**
     * Purpose: What this specific test checks
     * - Verifies that null, empty strings, and whitespace-only strings map to `null`.
     *
     * Why: Why this scenario matters
     * - Protects against unselected dropdowns or accidental blank text entries.
     *
     * Scenario: Input or condition being tested
     * - Inputs `null`, `''`, `'   '`.
     *
     * Pass condition: What result or assertion makes it pass
     * - All evaluate to `isNull`.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "CanonicalServiceCategory Mapping Safety null and empty/whitespace map to null"; Pop-Location
     */
    test('null and empty/whitespace map to null', () {
      expect(CanonicalServiceCategory.toCanonicalCategoryHint(null), isNull);
      expect(CanonicalServiceCategory.toCanonicalCategoryHint(''), isNull);
      expect(CanonicalServiceCategory.toCanonicalCategoryHint('   '), isNull);
    });

    /**
     * Purpose: What this specific test checks
     * - Verifies alias mapping from legacy or alternative term 'Vehicle Assistance' (case-insensitive) to canonical 'Vehicle Repair'.
     *
     * Why: Why this scenario matters
     * - Maintains backward compatibility with legacy UI labels or synonyms while standardizing on canonical backend category.
     *
     * Scenario: Input or condition being tested
     * - Inputs `'Vehicle Assistance'` and `'vehicle assistance'`.
     *
     * Pass condition: What result or assertion makes it pass
     * - Both evaluate to `'Vehicle Repair'`.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "CanonicalServiceCategory Mapping Safety Vehicle Assistance maps to Vehicle Repair"; Pop-Location
     */
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

    /**
     * Purpose: What this specific test checks
     * - Verifies that all valid canonical categories ('Plumbing', 'Electrical', 'Vehicle Repair', 'Appliance Repair') map directly to themselves.
     *
     * Why: Why this scenario matters
     * - Ensures valid categories selected by customers or AI are preserved without alteration.
     *
     * Scenario: Input or condition being tested
     * - Inputs for each canonical category name.
     *
     * Pass condition: What result or assertion makes it pass
     * - Each returns the identical canonical category string.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "CanonicalServiceCategory Mapping Safety canonical categories map to themselves"; Pop-Location
     */
    test('canonical categories map to themselves', () {
      expect(CanonicalServiceCategory.toCanonicalCategoryHint('Plumbing'), 'Plumbing');
      expect(CanonicalServiceCategory.toCanonicalCategoryHint('Electrical'), 'Electrical');
      expect(CanonicalServiceCategory.toCanonicalCategoryHint('Vehicle Repair'), 'Vehicle Repair');
      expect(CanonicalServiceCategory.toCanonicalCategoryHint('Appliance Repair'), 'Appliance Repair');
    });

    /**
     * Purpose: What this specific test checks
     * - Verifies that 'Unclassified' cannot be supplied as a category hint and throws `ArgumentError`.
     *
     * Why: Why this scenario matters
     * - 'Unclassified' is a system classification state, not a user selectable category hint.
     *
     * Scenario: Input or condition being tested
     * - Call with `'Unclassified'`.
     *
     * Pass condition: What result or assertion makes it pass
     * - Throws `ArgumentError`.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "CanonicalServiceCategory Mapping Safety Unclassified is rejected and throws ArgumentError"; Pop-Location
     */
    test('Unclassified is rejected and throws ArgumentError', () {
      expect(
        () => CanonicalServiceCategory.toCanonicalCategoryHint('Unclassified'),
        throwsArgumentError,
      );
    });

    /**
     * Purpose: What this specific test checks
     * - Verifies that 'Cleaning' is rejected and throws `ArgumentError`.
     *
     * Why: Why this scenario matters
     * - AssistLK currently supports Plumbing, Electrical, Vehicle Repair, and Appliance Repair; unsupported categories must be rejected early.
     *
     * Scenario: Input or condition being tested
     * - Call with `'Cleaning'`.
     *
     * Pass condition: What result or assertion makes it pass
     * - Throws `ArgumentError`.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "CanonicalServiceCategory Mapping Safety Cleaning is rejected and throws ArgumentError"; Pop-Location
     */
    test('Cleaning is rejected and throws ArgumentError', () {
      expect(
        () => CanonicalServiceCategory.toCanonicalCategoryHint('Cleaning'),
        throwsArgumentError,
      );
    });

    /**
     * Purpose: What this specific test checks
     * - Verifies that unsupported arbitrary category strings throw `ArgumentError`.
     *
     * Why: Why this scenario matters
     * - Enforces strict whitelist validation on client category hint submission.
     *
     * Scenario: Input or condition being tested
     * - Inputs `'AC Service'`, `'Vehicle'`, `'Emergency'`, `'random string'`.
     *
     * Pass condition: What result or assertion makes it pass
     * - All calls throw `ArgumentError`.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "CanonicalServiceCategory Mapping Safety arbitrary values are rejected and throw ArgumentError"; Pop-Location
     */
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

  /**
   * TEST GROUP: Structured Clarification Models and Lifecycle
   *
   * 1. What this group tests:
   *    - Verifies the structured clarification questions, problem analysis summaries, and lifecycle calculation rules on `ServiceRequestModel`.
   *    - Tests `ServiceRequestClarificationModel` JSON parsing, serialization, and actionable state properties (`isAnswered`, `isSuperseded`, `isActionable`).
   *    - Tests `ProblemAnalysisSummaryModel` JSON parsing and serialization of authoritative fields.
   *    - Tests `ServiceRequestModel` clarification lifecycle methods: `currentClarificationRound`, `currentRoundIsFullyAnswered`, `pendingQuestions`, and `hasReachedMaxRounds`.
   *    - Tests `ServiceRequestModel` matching expiry fields and computed `isMatchingExpired` property.
   * 2. Why these tests are needed:
   *    - Component 1 mobile clients drive an interactive multi-round clarification dialog with the customer.
   *      Correctly tracking round numbers, actionable vs superseded questions, answered statuses, and matching expiration is critical
   *      for user flow and provider matching eligibility.
   * 3. What is real and what is mocked:
   *    - Real Dart model deserialization, computed property evaluation, and JSON serialization. No mocks.
   * 4. Prerequisites: None.
   * 5. PowerShell command to run this group from repository root:
   *    Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "Structured Clarification Models and Lifecycle"; Pop-Location
   */
  group('Structured Clarification Models and Lifecycle', () {
    /**
     * Purpose: What this specific test checks
     * - Verifies complete round-trip JSON deserialization and serialization for `ServiceRequestClarificationModel`.
     *
     * Why: Why this scenario matters
     * - Clarification questions and answers are exchanged with the backend as JSON payloads.
     *
     * Scenario: Input or condition being tested
     * - Deserializing JSON containing id, round, sequence, question, answer, answeredAt, supersededAt.
     *
     * Pass condition: What result or assertion makes it pass
     * - Model fields match, `isAnswered` is true, `isSuperseded` is false, `isActionable` is false, and serialized JSON preserves keys.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "Structured Clarification Models and Lifecycle ServiceRequestClarificationModel parses from json and serializes to json"; Pop-Location
     */
    test('ServiceRequestClarificationModel parses from json and serializes to json', () {
      final json = {
        'id': 'c-1',
        'clarificationRound': 1,
        'sequence': 2,
        'question': 'Is there water leaking?',
        'answer': 'Yes, from underneath.',
        'answeredAt': '2026-09-10T12:00:00.000Z',
        'supersededAt': null,
      };

      final model = ServiceRequestClarificationModel.fromJson(json);
      expect(model.id, 'c-1');
      expect(model.clarificationRound, 1);
      expect(model.sequence, 2);
      expect(model.question, 'Is there water leaking?');
      expect(model.answer, 'Yes, from underneath.');
      expect(model.isAnswered, isTrue);
      expect(model.isSuperseded, isFalse);
      expect(model.isActionable, isFalse); // Answered, so not actionable

      final serialized = model.toJson();
      expect(serialized['id'], 'c-1');
      expect(serialized['answer'], 'Yes, from underneath.');
    });

    /**
     * Purpose: What this specific test checks
     * - Verifies the `isActionable` boolean property across pending and superseded states.
     *
     * Why: Why this scenario matters
     * - The UI only presents input fields for questions that are actionable (neither answered nor superseded).
     *
     * Scenario: Input or condition being tested
     * - Compare a pending question (no answer, not superseded) vs a superseded question (has `supersededAt`).
     *
     * Pass condition: What result or assertion makes it pass
     * - Pending question has `isActionable == true`; superseded question has `isSuperseded == true` and `isActionable == false`.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "Structured Clarification Models and Lifecycle ServiceRequestClarificationModel actionable state"; Pop-Location
     */
    test('ServiceRequestClarificationModel actionable state', () {
      final pending = const ServiceRequestClarificationModel(
        id: 'c-pending',
        clarificationRound: 1,
        sequence: 1,
        question: 'Any error code?',
      );
      expect(pending.isAnswered, isFalse);
      expect(pending.isSuperseded, isFalse);
      expect(pending.isActionable, isTrue);

      final superseded = ServiceRequestClarificationModel(
        id: 'c-superseded',
        clarificationRound: 1,
        sequence: 1,
        question: 'Any error code?',
        supersededAt: DateTime.now(),
      );
      expect(superseded.isAnswered, isFalse);
      expect(superseded.isSuperseded, isTrue);
      expect(superseded.isActionable, isFalse);
    });

    /**
     * Purpose: What this specific test checks
     * - Verifies JSON parsing and serialization for `ProblemAnalysisSummaryModel`.
     *
     * Why: Why this scenario matters
     * - Displays the AI agent's authoritative diagnosis (detected problem, confidence score, agent name) to the user.
     *
     * Scenario: Input or condition being tested
     * - Parse JSON with id, detectedProblem, confidence, agentName, createdAt.
     *
     * Pass condition: What result or assertion makes it pass
     * - Model properties match JSON values and serialized map preserves `confidence: 0.85`.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "Structured Clarification Models and Lifecycle ProblemAnalysisSummaryModel parses and serializes authoritative fields"; Pop-Location
     */
    test('ProblemAnalysisSummaryModel parses and serializes authoritative fields', () {
      final json = {
        'id': 'pa-1',
        'detectedProblem': 'Compressor failed',
        'confidence': 0.85,
        'agentName': 'ProblemUnderstandingAgent',
        'createdAt': '2026-09-10T12:00:00.000Z',
      };

      final model = ProblemAnalysisSummaryModel.fromJson(json);
      expect(model.id, 'pa-1');
      expect(model.detectedProblem, 'Compressor failed');
      expect(model.confidence, 0.85);
      expect(model.agentName, 'ProblemUnderstandingAgent');

      final serialized = model.toJson();
      expect(serialized['confidence'], 0.85);
    });

    /**
     * Purpose: What this specific test checks
     * - Verifies that `model.currentClarificationRound` resolves to `MAX(ClarificationRound)` even when all questions in that round have already been answered.
     *
     * Why: Why this scenario matters
     * - Discrepancy / design rule: "current round" represents the highest clarification round reached in the request lifecycle, NOT the lowest unanswered round.
     *
     * Scenario: Input or condition being tested
     * - Model with round 1 clarifications where both questions have answers.
     *
     * Pass condition: What result or assertion makes it pass
     * - `currentClarificationRound` is 1, `currentRoundIsFullyAnswered` is true, `pendingQuestions` is empty, and `hasReachedMaxRounds` is false.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "Structured Clarification Models and Lifecycle ServiceRequestModel lifecycle: current round is MAX(ClarificationRound) even when answered"; Pop-Location
     */
    test('ServiceRequestModel lifecycle: current round is MAX(ClarificationRound) even when answered', () {
      final model = ServiceRequestModel(
        serviceRequestId: 'req-1',
        customerId: 'cust-1',
        category: 'Appliance Repair',
        description: 'Fridge issue',
        locationText: 'Colombo',
        urgency: ServiceRequestUrgency.medium,
        status: ServiceRequestStatus.awaitingInformation,
        createdAt: DateTime.now(),
        updatedAt: DateTime.now(),
        clarifications: [
          const ServiceRequestClarificationModel(
            id: 'c-1',
            clarificationRound: 1,
            sequence: 1,
            question: 'Freezer cold?',
            answer: 'Yes, freezing cold',
          ),
          const ServiceRequestClarificationModel(
            id: 'c-2',
            clarificationRound: 1,
            sequence: 2,
            question: 'Any noise?',
            answer: 'Buzzing noise',
          ),
        ],
      );

      // Section 1: current round != unanswered round!
      // Must be 1 even though answered!
      expect(model.currentClarificationRound, 1);
      expect(model.currentRoundIsFullyAnswered, isTrue);
      expect(model.pendingQuestions, isEmpty);
      expect(model.hasReachedMaxRounds, isFalse);
    });

    /**
     * Purpose: What this specific test checks
     * - Verifies round 2 detection, pending question filtering, and max rounds threshold (`hasReachedMaxRounds == true`).
     *
     * Why: Why this scenario matters
     * - Component 1 limits clarification to 2 rounds maximum before concluding problem understanding.
     *
     * Scenario: Input or condition being tested
     * - Model with round 1 answered questions and round 2 pending question.
     *
     * Pass condition: What result or assertion makes it pass
     * - `currentClarificationRound` is 2, `currentRoundIsFullyAnswered` is false, `pendingQuestions` contains round 2 question, and `hasReachedMaxRounds` is true.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "Structured Clarification Models and Lifecycle ServiceRequestModel lifecycle: max rounds detection and pending separation"; Pop-Location
     */
    test('ServiceRequestModel lifecycle: max rounds detection and pending separation', () {
      final model = ServiceRequestModel(
        serviceRequestId: 'req-2',
        customerId: 'cust-1',
        category: 'Appliance Repair',
        description: 'Fridge issue',
        locationText: 'Colombo',
        urgency: ServiceRequestUrgency.medium,
        status: ServiceRequestStatus.awaitingInformation,
        createdAt: DateTime.now(),
        updatedAt: DateTime.now(),
        clarifications: [
          const ServiceRequestClarificationModel(
            id: 'c-r1-1',
            clarificationRound: 1,
            sequence: 1,
            question: 'Round 1 Q',
            answer: 'Round 1 A',
          ),
          const ServiceRequestClarificationModel(
            id: 'c-r2-1',
            clarificationRound: 2,
            sequence: 1,
            question: 'Round 2 Q',
            answer: null,
          ),
        ],
      );

      expect(model.currentClarificationRound, 2);
      expect(model.currentRoundIsFullyAnswered, isFalse);
      expect(model.pendingQuestions.length, 1);
      expect(model.pendingQuestions.first.id, 'c-r2-1');
      expect(model.hasReachedMaxRounds, isTrue);
    });

    /**
     * Purpose: What this specific test checks
     * - Verifies deserialization of `readyForMatchingAtUtc` and `matchingExpiresAtUtc` and the computation of `isMatchingExpired`.
     *
     * Why: Why this scenario matters
     * - Requests in `ReadyForMatching` status have a limited validity window (e.g., 24 hours) for provider matching before expiring.
     *
     * Scenario: Input or condition being tested
     * - Active request with future expiry vs expired request with past expiry.
     *
     * Pass condition: What result or assertion makes it pass
     * - Active model has `isMatchingEligible: true` and `isMatchingExpired: false`; expired model has `isMatchingEligible: false` and `isMatchingExpired: true`.
     *
     * Run from repository root:
     * Push-Location mobile; flutter test test/service_request_models_test.dart --plain-name "Structured Clarification Models and Lifecycle ServiceRequestModel parses matching expiry fields and computes isMatchingExpired"; Pop-Location
     */
    test('ServiceRequestModel parses matching expiry fields and computes isMatchingExpired', () {
      final now = DateTime.now().toUtc();
      final activeJson = {
        'serviceRequestId': 'req-active',
        'customerId': 'cust-1',
        'category': 'Plumbing',
        'description': 'Water leaking',
        'locationText': 'Colombo',
        'urgency': 'Medium',
        'status': 'ReadyForMatching',
        'readyForMatchingAtUtc': now.subtract(const Duration(hours: 2)).toIso8601String(),
        'matchingExpiresAtUtc': now.add(const Duration(hours: 22)).toIso8601String(),
        'isMatchingEligible': true,
        'createdAt': now.subtract(const Duration(days: 1)).toIso8601String(),
        'updatedAt': now.subtract(const Duration(hours: 2)).toIso8601String(),
      };

      final activeModel = ServiceRequestModel.fromJson(activeJson);
      expect(activeModel.isMatchingEligible, isTrue);
      expect(activeModel.isMatchingExpired, isFalse);
      expect(activeModel.readyForMatchingAtUtc, isNotNull);
      expect(activeModel.matchingExpiresAtUtc, isNotNull);

      final expiredJson = {
        'serviceRequestId': 'req-expired',
        'customerId': 'cust-1',
        'category': 'Plumbing',
        'description': 'Old water leaking',
        'locationText': 'Colombo',
        'urgency': 'Medium',
        'status': 'ReadyForMatching',
        'readyForMatchingAtUtc': now.subtract(const Duration(hours: 48)).toIso8601String(),
        'matchingExpiresAtUtc': now.subtract(const Duration(hours: 24)).toIso8601String(),
        'isMatchingEligible': false,
        'createdAt': now.subtract(const Duration(days: 3)).toIso8601String(),
        'updatedAt': now.subtract(const Duration(hours: 48)).toIso8601String(),
      };

      final expiredModel = ServiceRequestModel.fromJson(expiredJson);
      expect(expiredModel.isMatchingEligible, isFalse);
      expect(expiredModel.isMatchingExpired, isTrue);

      final serialized = activeModel.toJson();
      expect(serialized['isMatchingEligible'], isTrue);
      expect(serialized['readyForMatchingAtUtc'], isNotNull);
      expect(serialized['matchingExpiresAtUtc'], isNotNull);
    });
  });
}
