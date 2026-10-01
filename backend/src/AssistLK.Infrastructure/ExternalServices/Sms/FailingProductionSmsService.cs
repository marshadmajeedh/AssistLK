using AssistLK.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace AssistLK.Infrastructure.ExternalServices.Sms;

/// <summary>
/// Safety fallback service for non-Development environments when no real production SMS gateway is configured.
/// Safely throws an exception without disclosing internal credentials or faking verification.
/// </summary>
public class FailingProductionSmsService : ISmsService
{
    private readonly ILogger<FailingProductionSmsService> _logger;

    public FailingProductionSmsService(ILogger<FailingProductionSmsService> logger)
    {
        _logger = logger;
    }

    public Task SendOtpAsync(string phoneNumber, string otp, CancellationToken cancellationToken = default)
    {
        _logger.LogError("SMS delivery attempted in production environment without a configured SMS gateway provider.");
        throw new InvalidOperationException("SMS delivery service is unconfigured or unavailable in this environment.");
    }
}
