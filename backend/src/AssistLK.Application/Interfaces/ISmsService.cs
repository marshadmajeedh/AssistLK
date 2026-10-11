namespace AssistLK.Application.Interfaces;

public interface ISmsService
{
    /// <summary>
    /// Dispatches a one-time verification code to the target phone number.
    /// </summary>
    Task SendOtpAsync(string phoneNumber, string otp, CancellationToken cancellationToken = default);
}
