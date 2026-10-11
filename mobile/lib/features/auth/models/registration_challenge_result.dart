class RegistrationChallengeResult {
  final String challengeId;
  final String maskedPhoneNumber;
  final DateTime expiresAtUtc;
  final int cooldownSeconds;

  const RegistrationChallengeResult({
    required this.challengeId,
    required this.maskedPhoneNumber,
    required this.expiresAtUtc,
    this.cooldownSeconds = 45,
  });

  factory RegistrationChallengeResult.fromJson(Map<String, dynamic> json) {
    return RegistrationChallengeResult(
      challengeId: json['challengeId'] as String,
      maskedPhoneNumber: json['maskedPhoneNumber'] as String,
      expiresAtUtc: DateTime.parse(json['expiresAtUtc'] as String),
      cooldownSeconds: (json['cooldownSeconds'] as num?)?.toInt() ?? 45,
    );
  }
}
