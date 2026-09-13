class ServiceRequestAttachment {
  const ServiceRequestAttachment({
    required this.id,
    required this.slot,
    required this.contentType,
    required this.fileSizeBytes,
    required this.width,
    required this.height,
    required this.createdAt,
  });
  final String id;
  final int slot;
  final String contentType;
  final int fileSizeBytes;
  final int width;
  final int height;
  final DateTime createdAt;

  factory ServiceRequestAttachment.fromJson(Map<String, dynamic> json) =>
      ServiceRequestAttachment(
        id: json['id'] as String,
        slot: json['slot'] as int,
        contentType: json['contentType'] as String,
        fileSizeBytes: json['fileSizeBytes'] as int,
        width: json['width'] as int,
        height: json['height'] as int,
        createdAt: DateTime.parse(json['createdAt'] as String),
      );
}
