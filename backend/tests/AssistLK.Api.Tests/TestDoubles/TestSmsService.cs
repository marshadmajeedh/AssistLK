using AssistLK.Application.Interfaces;

namespace AssistLK.Api.Tests.TestDoubles;

public class TestSmsService : ISmsService
{
    public string? LastPhoneNumber { get; set; }
    public string? LastOtp { get; set; }
    public int SendCount { get; set; }
    public bool ShouldFail { get; set; }

    public Task SendOtpAsync(string phoneNumber, string otp, CancellationToken cancellationToken = default)
    {
        if (ShouldFail)
        {
            throw new Exception("Simulated SMS gateway network failure");
        }

        LastPhoneNumber = phoneNumber;
        LastOtp = otp;
        SendCount++;
        return Task.CompletedTask;
    }

    public void Reset()
    {
        LastPhoneNumber = null;
        LastOtp = null;
        SendCount = 0;
        ShouldFail = false;
    }
}
