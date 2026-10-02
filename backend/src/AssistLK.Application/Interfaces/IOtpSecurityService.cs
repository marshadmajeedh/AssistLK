namespace AssistLK.Application.Interfaces;

public interface IOtpSecurityService
{
    /// <summary>
    /// Generates a cryptographically secure 6-digit numeric OTP.
    /// </summary>
    string GenerateOtp();

    /// <summary>
    /// Computes an HMAC-SHA256 hash using the server-held pepper for a given challenge ID and OTP.
    /// </summary>
    string ComputeOtpHash(Guid challengeId, string otp);

    /// <summary>
    /// Verifies the candidate OTP against the stored hash in constant time.
    /// </summary>
    bool VerifyOtp(Guid challengeId, string candidateOtp, string storedHash);
}
