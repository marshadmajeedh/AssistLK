import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/quotations/models/booking_model.dart';

void main() {
  test('parses provider booking service-job GUID id', () {
    const id = '8ba82f76-a66d-4c4f-8ae3-3bc594d54b6c';

    final booking = ProviderBookingSummaryModel.fromJson({
      'id': id,
      'serviceRequestId': 'a650ff0b-e68a-4e6c-9e32-4c7b02b04ee8',
      'providerId': 'cc1898cc-f4c5-4797-9be3-243daae6d675',
      'customerName': 'Customer',
      'serviceTitle': 'Repair',
      'locationText': 'Colombo',
      'amount': 2500.0,
      'statusLabel': 'ASSIGNED',
      'statusCode': 1,
      'scheduledAt': '2026-10-11T09:00:00Z',
      'isCompleted': false,
      'isPendingQuote': false,
    });

    expect(booking.id, id);
  });
}
