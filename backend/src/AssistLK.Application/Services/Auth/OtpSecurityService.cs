using System.Security.Cryptography;
using System.Text;
using AssistLK.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace AssistLK.Application.Services.Auth;

public class OtpSecurityService : IOtpSecurityService
{
    private const int MinimumPepperLength = 16;
    private readonly byte[] _pepperBytes;

    public OtpSecurityService(IConfiguration configuration)
    {
        var pepper = configuration["AuthOtp:OtpPepper"];
        if (string.IsNullOrWhiteSpace(pepper))
        {
            throw new InvalidOperationException(
                "AuthOtp:OtpPepper is not configured. An explicit pepper must be provided via dotnet user-secrets or environment variable 'AuthOtp__OtpPepper'.");
        }

        var trimmed = pepper.Trim();
        if (trimmed.Length < MinimumPepperLength)
        {
            throw new InvalidOperationException(
                $"AuthOtp:OtpPepper is too weak. The pepper must contain at least {MinimumPepperLength} characters.");
        }

        _pepperBytes = Encoding.UTF8.GetBytes(trimmed);
    }

    public string GenerateOtp()
    {
        var code = RandomNumberGenerator.GetInt32(100000, 1000000);
        return code.ToString("D6");
    }

    public string ComputeOtpHash(Guid challengeId, string otp)
    {
        var payload = Encoding.UTF8.GetBytes($"{challengeId:N}:{otp.Trim()}");
        using var hmac = new HMACSHA256(_pepperBytes);
        var hash = hmac.ComputeHash(payload);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public bool VerifyOtp(Guid challengeId, string candidateOtp, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(candidateOtp) || string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        var computed = ComputeOtpHash(challengeId, candidateOtp);
        var computedBytes = Encoding.UTF8.GetBytes(computed);
        var storedBytes = Encoding.UTF8.GetBytes(storedHash.Trim().ToLowerInvariant());

        if (computedBytes.Length != storedBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(computedBytes, storedBytes);
    }
}
