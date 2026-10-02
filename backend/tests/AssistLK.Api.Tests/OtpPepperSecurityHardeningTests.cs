using System.Security.Cryptography;
using System.Text;
using AssistLK.Application.Services.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AssistLK.Api.Tests;

[Collection("EnvironmentTests")]
public class OtpPepperSecurityHardeningTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OtpSecurityService_Throws_When_Pepper_Is_Missing(string? missingPepper)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthOtp:OtpPepper"] = missingPepper
            })
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() => new OtpSecurityService(config));
        Assert.Contains("AuthOtp:OtpPepper is not configured", ex.Message);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("12345678")]
    [InlineData("short_pepper_15")] // 15 chars
    public void OtpSecurityService_Throws_When_Pepper_Is_Too_Weak(string weakPepper)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthOtp:OtpPepper"] = weakPepper
            })
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(() => new OtpSecurityService(config));
        Assert.Contains("AuthOtp:OtpPepper is too weak", ex.Message);
    }

    [Fact]
    public void OtpSecurityService_Succeeds_When_Pepper_Is_Valid()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthOtp:OtpPepper"] = "ExplicitSecurePepper123456!"
            })
            .Build();

        var service = new OtpSecurityService(config);
        Assert.NotNull(service);

        var otp = service.GenerateOtp();
        Assert.Equal(6, otp.Length);
        Assert.True(int.TryParse(otp, out _));
    }

    [Fact]
    public void OtpVerification_ConsistentAcrossServiceInstances_WithSamePepper()
    {
        var pepper = "DeterministicPepperForRestartSafety123!";
        var configA = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AuthOtp:OtpPepper"] = pepper })
            .Build();
        var configB = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AuthOtp:OtpPepper"] = pepper })
            .Build();

        var instanceBeforeRestart = new OtpSecurityService(configA);
        var instanceAfterRestart = new OtpSecurityService(configB);

        var challengeId = Guid.NewGuid();
        var otp = "654321";

        // Instance A computes hash before restart
        var storedHash = instanceBeforeRestart.ComputeOtpHash(challengeId, otp);

        // Instance B verifies hash after restart
        var verified = instanceAfterRestart.VerifyOtp(challengeId, otp, storedHash);
        Assert.True(verified, "Challenges must remain verifiable across backend restarts when same pepper is configured.");
    }

    [Fact]
    public void OtpVerification_Fails_AcrossInstances_WithDifferentPepper()
    {
        var configA = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AuthOtp:OtpPepper"] = "FirstPepperKey123456789012345" })
            .Build();
        var configB = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AuthOtp:OtpPepper"] = "SecondPepperKey12345678901234" })
            .Build();

        var instanceA = new OtpSecurityService(configA);
        var instanceB = new OtpSecurityService(configB);

        var challengeId = Guid.NewGuid();
        var otp = "123456";

        var storedHash = instanceA.ComputeOtpHash(challengeId, otp);
        var verified = instanceB.VerifyOtp(challengeId, otp, storedHash);

        Assert.False(verified, "Verification must fail if pepper differs.");
    }

    [Fact]
    public void Startup_FailsFast_When_AuthOtpPepper_Is_Missing()
    {
        var originalPepper = Environment.GetEnvironmentVariable("AuthOtp__OtpPepper");
        try
        {
            using var factory = new AssistLKApiTestFactory();
            Environment.SetEnvironmentVariable("AuthOtp__OtpPepper", null);
            var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
            Assert.Contains("AuthOtp:OtpPepper is not configured", ex.ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("AuthOtp__OtpPepper", originalPepper);
        }
    }

    [Fact]
    public void Startup_FailsFast_When_AuthOtpPepper_Is_Too_Weak()
    {
        var originalPepper = Environment.GetEnvironmentVariable("AuthOtp__OtpPepper");
        try
        {
            using var factory = new AssistLKApiTestFactory();
            Environment.SetEnvironmentVariable("AuthOtp__OtpPepper", "short_weak");
            var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
            Assert.Contains("AuthOtp:OtpPepper is too weak", ex.ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("AuthOtp__OtpPepper", originalPepper);
        }
    }
}
