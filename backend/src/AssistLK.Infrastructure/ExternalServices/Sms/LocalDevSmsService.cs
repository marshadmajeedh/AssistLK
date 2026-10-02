using AssistLK.Application.Common;
using AssistLK.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace AssistLK.Infrastructure.ExternalServices.Sms;

/// <summary>
/// Development-only SMS service that outputs OTPs to the developer console and logger.
/// Must NEVER be registered or used in non-Development environments.
/// </summary>
public class LocalDevSmsService : ISmsService
{
    private readonly ILogger<LocalDevSmsService> _logger;

    public LocalDevSmsService(ILogger<LocalDevSmsService> logger)
    {
        _logger = logger;
    }

    public Task SendOtpAsync(string phoneNumber, string otp, CancellationToken cancellationToken = default)
    {
        var maskedPhone = PhoneNumberNormalizer.Mask(phoneNumber);

        // Explicit console output for immediate developer visibility during local workflows
        Console.WriteLine($"[DEV ONLY - OTP] Code {otp} sent to {maskedPhone}");

        _logger.LogInformation("[DEV ONLY - OTP] Code {Otp} sent to {MaskedPhone}", otp, maskedPhone);

        return Task.CompletedTask;
    }
}
