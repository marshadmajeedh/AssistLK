import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';

import '../../../shared/theme/app_colors.dart';
import '../../../shared/theme/app_radius.dart';
import '../../../shared/theme/app_spacing.dart';
import '../../../shared/theme/app_text_styles.dart';
import '../../../shared/widgets/app_button.dart';
import '../models/registration_challenge_result.dart';
import '../providers/auth_provider.dart';

class CustomerPhoneOtpScreen extends StatefulWidget {
  final RegistrationChallengeResult challenge;

  const CustomerPhoneOtpScreen({
    super.key,
    required this.challenge,
  });

  @override
  State<CustomerPhoneOtpScreen> createState() => _CustomerPhoneOtpScreenState();
}

class _CustomerPhoneOtpScreenState extends State<CustomerPhoneOtpScreen> {
  late final TextEditingController _otpController;
  late final FocusNode _focusNode;
  Timer? _timer;
  late int _secondsRemaining;
  bool _isResending = false;

  @override
  void initState() {
    super.initState();
    _otpController = TextEditingController();
    _focusNode = FocusNode();
    _secondsRemaining = widget.challenge.cooldownSeconds;
    _startTimer();
  }

  void _startTimer() {
    _timer?.cancel();
    if (_secondsRemaining <= 0) return;

    _timer = Timer.periodic(const Duration(seconds: 1), (timer) {
      if (!mounted) {
        timer.cancel();
        return;
      }
      setState(() {
        if (_secondsRemaining > 1) {
          _secondsRemaining--;
        } else {
          _secondsRemaining = 0;
          timer.cancel();
        }
      });
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    _otpController.dispose();
    _focusNode.dispose();
    super.dispose();
  }

  Future<void> _verify() async {
    final otp = _otpController.text.trim();
    if (otp.length != 6) {
      return;
    }

    final auth = context.read<AuthProvider>();
    final success = await auth.verifyRegisterOtp(
      challengeId: widget.challenge.challengeId,
      otp: otp,
    );

    if (success && mounted) {
      // Pop all auth routes to root. AuthGate will show CustomerAppShell.
      Navigator.of(context).popUntil((route) => route.isFirst);
    }
  }

  Future<void> _resend() async {
    if (_secondsRemaining > 0 || _isResending) return;

    setState(() {
      _isResending = true;
    });

    final auth = context.read<AuthProvider>();
    final cooldown = await auth.resendRegisterOtp(
      challengeId: widget.challenge.challengeId,
    );

    if (mounted) {
      setState(() {
        _isResending = false;
        if (cooldown != null && cooldown > 0) {
          _secondsRemaining = cooldown;
          _startTimer();
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              content: Text('A new verification code has been sent to your phone.'),
              backgroundColor: AppColors.primary,
            ),
          );
        }
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();
    final otpText = _otpController.text;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Verify Phone'),
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          onPressed: () {
            auth.clearError();
            Navigator.of(context).pop();
          },
        ),
      ),
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.symmetric(
              horizontal: AppSpacing.lg,
              vertical: AppSpacing.md,
            ),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 480),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const SizedBox(height: AppSpacing.md),
                  // Icon
                  Center(
                    child: Container(
                      width: 72,
                      height: 72,
                      decoration: BoxDecoration(
                        color: AppColors.primarySurface,
                        shape: BoxShape.circle,
                      ),
                      child: const Icon(
                        Icons.mark_email_read_outlined,
                        color: AppColors.primary,
                        size: 36,
                      ),
                    ),
                  ),
                  const SizedBox(height: AppSpacing.lg),

                  // Header
                  const Text(
                    'Verify your phone',
                    style: AppTextStyles.pageTitle,
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: AppSpacing.xs),
                  Text(
                    'We sent a 6-digit verification code to\n${widget.challenge.maskedPhoneNumber}',
                    style: const TextStyle(
                      fontSize: 14,
                      color: AppColors.textSecondary,
                      height: 1.5,
                    ),
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: AppSpacing.xl),

                  // Card
                  Card(
                    elevation: 0,
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(AppRadius.large),
                      side: const BorderSide(color: AppColors.border),
                    ),
                    child: Padding(
                      padding: const EdgeInsets.all(AppSpacing.lg),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          // 6-digit OTP input boxes
                          GestureDetector(
                            onTap: () => _focusNode.requestFocus(),
                            child: Stack(
                              alignment: Alignment.center,
                              children: [
                                // Hidden authoritative textfield
                                Opacity(
                                  opacity: 0,
                                  child: TextField(
                                    controller: _otpController,
                                    focusNode: _focusNode,
                                    keyboardType: TextInputType.number,
                                    autofocus: true,
                                    maxLength: 6,
                                    autofillHints: const [AutofillHints.oneTimeCode],
                                    inputFormatters: [
                                      FilteringTextInputFormatter.digitsOnly,
                                    ],
                                    onChanged: (val) {
                                      setState(() {});
                                      if (val.length == 6) {
                                        _verify();
                                      }
                                    },
                                  ),
                                ),
                                // 6 visual boxes
                                Row(
                                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                  children: List.generate(6, (index) {
                                    final isFilled = index < otpText.length;
                                    final isCurrent = index == otpText.length && _focusNode.hasFocus;
                                    final digit = isFilled ? otpText[index] : '';

                                    return Container(
                                      width: 46,
                                      height: 54,
                                      decoration: BoxDecoration(
                                        color: isCurrent
                                            ? AppColors.primarySurface
                                            : AppColors.surface,
                                        borderRadius: BorderRadius.circular(AppRadius.medium),
                                        border: Border.all(
                                          color: isCurrent
                                              ? AppColors.primary
                                              : (isFilled ? AppColors.textPrimary : AppColors.border),
                                          width: isCurrent ? 2 : 1.2,
                                        ),
                                      ),
                                      alignment: Alignment.center,
                                      child: Text(
                                        digit,
                                        style: const TextStyle(
                                          fontSize: 22,
                                          fontWeight: FontWeight.w700,
                                          color: AppColors.textPrimary,
                                        ),
                                      ),
                                    );
                                  }),
                                ),
                              ],
                            ),
                          ),
                          const SizedBox(height: AppSpacing.md),

                          // Error display
                          if (auth.error != null) ...[
                            Container(
                              padding: const EdgeInsets.all(AppSpacing.sm),
                              decoration: BoxDecoration(
                                color: AppColors.error.withValues(alpha: 0.1),
                                borderRadius: BorderRadius.circular(
                                  AppRadius.small,
                                ),
                              ),
                              child: Row(
                                children: [
                                  const Icon(
                                    Icons.error_outline,
                                    color: AppColors.error,
                                    size: 18,
                                  ),
                                  const SizedBox(width: AppSpacing.xs),
                                  Expanded(
                                    child: Text(
                                      auth.error!,
                                      style: const TextStyle(
                                        color: AppColors.error,
                                        fontSize: 13,
                                      ),
                                    ),
                                  ),
                                ],
                              ),
                            ),
                            const SizedBox(height: AppSpacing.md),
                          ],

                          // Primary action: Verify & Create Account
                          AppButton(
                            text: 'Verify & Create Account',
                            isLoading: auth.isLoading,
                            onPressed: (otpText.length == 6 && !auth.isLoading)
                                ? _verify
                                : null,
                          ),
                          const SizedBox(height: AppSpacing.md),

                          // Resend action with countdown
                          Center(
                            child: _secondsRemaining > 0
                                ? Text(
                                    'Resend code in ${_secondsRemaining}s',
                                    style: const TextStyle(
                                      fontSize: 14,
                                      color: AppColors.textSecondary,
                                      fontWeight: FontWeight.w500,
                                    ),
                                  )
                                : TextButton(
                                    onPressed: (_isResending || auth.isLoading) ? null : _resend,
                                    child: _isResending
                                        ? const SizedBox(
                                            width: 16,
                                            height: 16,
                                            child: CircularProgressIndicator(strokeWidth: 2),
                                          )
                                        : const Text(
                                            'Resend code',
                                            style: TextStyle(
                                              fontWeight: FontWeight.w600,
                                            ),
                                          ),
                                  ),
                          ),
                          const SizedBox(height: AppSpacing.xs),

                          // Secondary action: Change phone number
                          Center(
                            child: TextButton(
                              onPressed: () {
                                auth.clearError();
                                Navigator.of(context).pop();
                              },
                              child: const Text(
                                'Change phone number',
                                style: TextStyle(
                                  color: AppColors.textSecondary,
                                  fontSize: 13,
                                ),
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
