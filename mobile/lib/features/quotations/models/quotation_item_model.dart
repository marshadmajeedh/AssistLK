/// A single line item inside a quotation (e.g., "Visit charge").
class QuotationItemModel {
  final int id;
  final String description;
  final double amount;
  final int quantity;

  const QuotationItemModel({
    required this.id,
    required this.description,
    required this.amount,
    required this.quantity,
  });

  double get lineTotal => amount * quantity;

  factory QuotationItemModel.fromJson(Map<String, dynamic> json) {
    return QuotationItemModel(
      id: (json['id'] as num?)?.toInt() ?? 0,
      description: json['description'] as String? ?? '',
      amount: (json['amount'] as num?)?.toDouble() ?? 0.0,
      quantity: (json['quantity'] as num?)?.toInt() ?? 0,
    );
  }

  Map<String, dynamic> toJson() => {
        'id': id,
        'description': description,
        'amount': amount,
        'quantity': quantity,
      };
}