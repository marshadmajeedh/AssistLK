using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AssistLK.Application.Auth.DTOs;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using AssistLK.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AssistLK.Api.Tests;

public class CustomerOtpRegistrationTests : IClassFixture<AssistLKApiTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly AssistLKApiTestFactory _factory;
    private readonly HttpClient _client;

    public CustomerOtpRegistrationTests(AssistLKApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.TestSms.Reset();
    }

    private Task<HttpResponseMessage> PostJsonAsync<T>(string uri, T value)
    {
        return _client.PostAsJsonAsync(uri, value, JsonOptions);
    }

    [Fact]
    public async Task RegisterStart_ValidCustomer_Returns200WithMaskedPhoneAndChallengeId_NoUser_NoJwt_NoPlaintextOtp()
    {
        // Arrange
        _factory.TestSms.Reset();
        var email = $"newcustomer_{Guid.NewGuid()}@assistlk.com";
        var request = new RegisterStartRequest
        {
            FullName = "Amila Perera",
            Email = email,
            Password = "Password123!",
            PhoneNumber = "0771234567",
            Role = UserRole.Customer
        };

        // Act
        var response = await PostJsonAsync("/api/auth/register/start", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<RegisterStartResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.ChallengeId);
        Assert.Equal("+94 77 *** *567", result.MaskedPhoneNumber);
        Assert.Equal(45, result.CooldownSeconds);

        // Security assertions: no JWT, no OTP in response
        var rawJson = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("token", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("jwt", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(_factory.TestSms.LastOtp!, rawJson);

        // Database assertion: User must NOT be created before OTP verification
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AssistLKDbContext>();
        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == email);
        Assert.Null(user);

        // Database assertion: Challenge must exist, but plaintext OTP must NOT be stored
        var challenge = await context.RegistrationChallenges.FirstOrDefaultAsync(c => c.Id == result.ChallengeId);
        Assert.NotNull(challenge);
        Assert.NotEqual(_factory.TestSms.LastOtp, challenge.OtpHash);
        Assert.False(challenge.IsConsumed);
        Assert.Equal("+94771234567", challenge.PhoneNumber);
    }

    [Fact]
    public async Task RegisterStart_RequiresPhone_Returns400()
    {
        var request = new RegisterStartRequest
        {
            FullName = "Amila Perera",
            Email = $"nophone_{Guid.NewGuid()}@assistlk.com",
            Password = "Password123!",
            PhoneNumber = "",
            Role = UserRole.Customer
        };

        var response = await PostJsonAsync("/api/auth/register/start", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("0112345678")] // Landline
    [InlineData("077123")] // Too short
    [InlineData("0771234567890")] // Too long
    [InlineData("077abcd123")] // Non numeric
    public async Task RegisterStart_InvalidPhoneFormat_Returns400(string invalidPhone)
    {
        var request = new RegisterStartRequest
        {
            FullName = "Amila Perera",
            Email = $"invalidphone_{Guid.NewGuid()}@assistlk.com",
            Password = "Password123!",
            PhoneNumber = invalidPhone,
            Role = UserRole.Customer
        };

        var response = await PostJsonAsync("/api/auth/register/start", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterStart_DuplicateEmail_Returns409()
    {
        var email = $"dupemail_{Guid.NewGuid()}@assistlk.com";
        await _factory.SeedAsync(async context =>
        {
            context.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                FullName = "Existing User",
                Email = email,
                PasswordHash = "hash",
                PhoneNumber = "+94770000001",
                Role = UserRole.Customer,
                IsActive = true
            });
            await Task.CompletedTask;
        });

        var request = new RegisterStartRequest
        {
            FullName = "New User",
            Email = email,
            Password = "Password123!",
            PhoneNumber = "0771234567",
            Role = UserRole.Customer
        };

        var response = await PostJsonAsync("/api/auth/register/start", request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RegisterStart_VerifiedCustomerPhoneConflict_Returns409()
    {
        await _factory.SeedAsync(async context =>
        {
            context.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                FullName = "Verified Customer",
                Email = $"verified_{Guid.NewGuid()}@assistlk.com",
                PasswordHash = "hash",
                PhoneNumber = "+94771112233",
                Role = UserRole.Customer,
                IsActive = true,
                IsPhoneVerified = true,
                PhoneVerifiedAtUtc = DateTime.UtcNow
            });
            await Task.CompletedTask;
        });

        var request = new RegisterStartRequest
        {
            FullName = "New Requester",
            Email = $"newguy_{Guid.NewGuid()}@assistlk.com",
            Password = "Password123!",
            PhoneNumber = "0771112233", // Same phone in 07X format
            Role = UserRole.Customer
        };

        var response = await PostJsonAsync("/api/auth/register/start", request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RegisterStart_LegacyUnverifiedDuplicatePhone_AllowsNewChallenge()
    {
        var legacyPhone = "0772223344";
        await _factory.SeedAsync(async context =>
        {
            context.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                FullName = "Legacy Unverified Customer",
                Email = $"legacy_{Guid.NewGuid()}@assistlk.com",
                PasswordHash = "hash",
                PhoneNumber = legacyPhone,
                Role = UserRole.Customer,
                IsActive = true,
                IsPhoneVerified = false,
                PhoneVerifiedAtUtc = null
            });
            await Task.CompletedTask;
        });

        var request = new RegisterStartRequest
        {
            FullName = "Legitimate Owner",
            Email = $"owner_{Guid.NewGuid()}@assistlk.com",
            Password = "Password123!",
            PhoneNumber = legacyPhone,
            Role = UserRole.Customer
        };

        var response = await PostJsonAsync("/api/auth/register/start", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RegisterStart_RejectsProviderRole_Returns400()
    {
        var request = new RegisterStartRequest
        {
            FullName = "Provider Person",
            Email = $"provider_{Guid.NewGuid()}@assistlk.com",
            Password = "Password123!",
            PhoneNumber = "0771234567",
            Role = UserRole.Provider
        };

        var response = await PostJsonAsync("/api/auth/register/start", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LegacyRegister_CustomerCannotBypassOtp_Returns400()
    {
        var email = $"bypass_{Guid.NewGuid()}@assistlk.com";
        var request = new RegisterRequest
        {
            FullName = "Bypass Attempter",
            Email = email,
            Password = "Password123!",
            PhoneNumber = "0771234567",
            Role = UserRole.Customer
        };

        var response = await PostJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("phone verification", body, StringComparison.OrdinalIgnoreCase);

        // Verify user was NOT created
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AssistLKDbContext>();
        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == email);
        Assert.Null(user);
    }

    [Fact]
    public async Task LegacyRegister_AdminStillForbidden_Returns400()
    {
        var request = new RegisterRequest
        {
            FullName = "Admin Attempter",
            Email = $"admin_{Guid.NewGuid()}@assistlk.com",
            Password = "Password123!",
            PhoneNumber = "0771234567",
            Role = UserRole.Admin
        };

        var response = await PostJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task VerifyOtp_CorrectCode_CreatesUser_ConsumesChallenge_ReturnsJwt()
    {
        // 1. Start registration
        _factory.TestSms.Reset();
        var email = $"verify_success_{Guid.NewGuid()}@assistlk.com";
        var startRequest = new RegisterStartRequest
        {
            FullName = "Nimal Silva",
            Email = email,
            Password = "Password123!",
            PhoneNumber = "0773334455",
            Role = UserRole.Customer
        };

        var startResponse = await PostJsonAsync("/api/auth/register/start", startRequest);
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        var startResult = await startResponse.Content.ReadFromJsonAsync<RegisterStartResponse>(JsonOptions);
        Assert.NotNull(startResult);

        var otp = _factory.TestSms.LastOtp;
        Assert.NotNull(otp);

        // 2. Verify OTP
        var verifyRequest = new VerifyOtpRequest
        {
            ChallengeId = startResult.ChallengeId,
            Otp = otp
        };

        var verifyResponse = await PostJsonAsync("/api/auth/register/verify-otp", verifyRequest);
        Assert.Equal(HttpStatusCode.Created, verifyResponse.StatusCode);

        var authResult = await verifyResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        Assert.NotNull(authResult);
        Assert.False(string.IsNullOrWhiteSpace(authResult.Token));
        Assert.Equal(email, authResult.Email);
        Assert.Equal("Customer", authResult.Role);

        // 3. Inspect database state
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AssistLKDbContext>();

        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == email);
        Assert.NotNull(user);
        Assert.True(user.IsActive);
        Assert.True(user.IsPhoneVerified);
        Assert.NotNull(user.PhoneVerifiedAtUtc);
        Assert.Equal("+94773334455", user.PhoneNumber);

        var challenge = await context.RegistrationChallenges.FirstOrDefaultAsync(c => c.Id == startResult.ChallengeId);
        Assert.NotNull(challenge);
        Assert.True(challenge.IsConsumed);
    }

    [Fact]
    public async Task VerifyOtp_WrongCode_IncrementsAttempts_NoUserCreated_Returns400()
    {
        _factory.TestSms.Reset();
        var email = $"wrong_otp_{Guid.NewGuid()}@assistlk.com";
        var startRequest = new RegisterStartRequest
        {
            FullName = "Sunil Shantha",
            Email = email,
            Password = "Password123!",
            PhoneNumber = "0774445566",
            Role = UserRole.Customer
        };

        var startResponse = await PostJsonAsync("/api/auth/register/start", startRequest);
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        var startResult = await startResponse.Content.ReadFromJsonAsync<RegisterStartResponse>(JsonOptions);

        var verifyRequest = new VerifyOtpRequest
        {
            ChallengeId = startResult!.ChallengeId,
            Otp = "000000" // Wrong code
        };

        var verifyResponse = await PostJsonAsync("/api/auth/register/verify-otp", verifyRequest);
        Assert.Equal(HttpStatusCode.BadRequest, verifyResponse.StatusCode);

        var body = await verifyResponse.Content.ReadAsStringAsync();
        Assert.Contains("4 attempts remaining", body);

        // User not created
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AssistLKDbContext>();
        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == email);
        Assert.Null(user);

        var challenge = await context.RegistrationChallenges.FindAsync(startResult.ChallengeId);
        Assert.NotNull(challenge);
        Assert.Equal(1, challenge.AttemptCount);
        Assert.False(challenge.IsConsumed);
    }

    [Fact]
    public async Task VerifyOtp_MaxAttemptsReached_LocksChallenge_Returns400()
    {
        _factory.TestSms.Reset();
        var email = $"max_attempts_{Guid.NewGuid()}@assistlk.com";
        var startRequest = new RegisterStartRequest
        {
            FullName = "Max Attempter",
            Email = email,
            Password = "Password123!",
            PhoneNumber = "0775556677",
            Role = UserRole.Customer
        };

        var startResponse = await PostJsonAsync("/api/auth/register/start", startRequest);
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        var startResult = await startResponse.Content.ReadFromJsonAsync<RegisterStartResponse>(JsonOptions);
        var correctOtp = _factory.TestSms.LastOtp!;

        // 5 wrong attempts
        for (int i = 0; i < 5; i++)
        {
            await PostJsonAsync("/api/auth/register/verify-otp", new VerifyOtpRequest
            {
                ChallengeId = startResult!.ChallengeId,
                Otp = "111111"
            });
        }

        // 6th attempt with correct OTP must be locked
        var lockedResponse = await PostJsonAsync("/api/auth/register/verify-otp", new VerifyOtpRequest
        {
            ChallengeId = startResult!.ChallengeId,
            Otp = correctOtp
        });

        Assert.Equal(HttpStatusCode.BadRequest, lockedResponse.StatusCode);
        var body = await lockedResponse.Content.ReadAsStringAsync();
        Assert.Contains("exceeded", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifyOtp_ExpiredChallenge_Returns400()
    {
        _factory.TestSms.Reset();
        var email = $"expired_{Guid.NewGuid()}@assistlk.com";
        var startRequest = new RegisterStartRequest
        {
            FullName = "Late Customer",
            Email = email,
            Password = "Password123!",
            PhoneNumber = "0776667788",
            Role = UserRole.Customer
        };

        var startResponse = await PostJsonAsync("/api/auth/register/start", startRequest);
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        var startResult = await startResponse.Content.ReadFromJsonAsync<RegisterStartResponse>(JsonOptions);
        var otp = _factory.TestSms.LastOtp!;

        // Expire challenge in DB
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AssistLKDbContext>();
            var challenge = await context.RegistrationChallenges.FindAsync(startResult!.ChallengeId);
            challenge!.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-10);
            await context.SaveChangesAsync();
        }

        var verifyResponse = await PostJsonAsync("/api/auth/register/verify-otp", new VerifyOtpRequest
        {
            ChallengeId = startResult!.ChallengeId,
            Otp = otp
        });

        Assert.Equal(HttpStatusCode.BadRequest, verifyResponse.StatusCode);
        var body = await verifyResponse.Content.ReadAsStringAsync();
        Assert.Contains("expired", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifyOtp_AlreadyConsumedChallenge_Returns400()
    {
        _factory.TestSms.Reset();
        var email = $"already_consumed_{Guid.NewGuid()}@assistlk.com";
        var startRequest = new RegisterStartRequest
        {
            FullName = "Double Verifier",
            Email = email,
            Password = "Password123!",
            PhoneNumber = "0777778899",
            Role = UserRole.Customer
        };

        var startResponse = await PostJsonAsync("/api/auth/register/start", startRequest);
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        var startResult = await startResponse.Content.ReadFromJsonAsync<RegisterStartResponse>(JsonOptions);
        var otp = _factory.TestSms.LastOtp!;

        // Verify once -> 201 Created
        var firstVerify = await PostJsonAsync("/api/auth/register/verify-otp", new VerifyOtpRequest
        {
            ChallengeId = startResult!.ChallengeId,
            Otp = otp
        });
        Assert.Equal(HttpStatusCode.Created, firstVerify.StatusCode);

        // Verify second time -> 400 Bad Request
        var secondVerify = await PostJsonAsync("/api/auth/register/verify-otp", new VerifyOtpRequest
        {
            ChallengeId = startResult.ChallengeId,
            Otp = otp
        });
        Assert.Equal(HttpStatusCode.BadRequest, secondVerify.StatusCode);
        var body = await secondVerify.Content.ReadAsStringAsync();
        Assert.Contains("already been completed", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResendOtp_CooldownEnforced_Returns400()
    {
        _factory.TestSms.Reset();
        var startRequest = new RegisterStartRequest
        {
            FullName = "Rapid Clicker",
            Email = $"cooldown_{Guid.NewGuid()}@assistlk.com",
            Password = "Password123!",
            PhoneNumber = "0778889900",
            Role = UserRole.Customer
        };

        var startResponse = await PostJsonAsync("/api/auth/register/start", startRequest);
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        var startResult = await startResponse.Content.ReadFromJsonAsync<RegisterStartResponse>(JsonOptions);

        // Immediate resend -> should be rejected under 45-second cooldown
        var resendResponse = await PostJsonAsync("/api/auth/register/resend-otp", new ResendOtpRequest
        {
            ChallengeId = startResult!.ChallengeId
        });

        Assert.Equal(HttpStatusCode.BadRequest, resendResponse.StatusCode);
        var body = await resendResponse.Content.ReadAsStringAsync();
        Assert.Contains("Please wait", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResendOtp_AfterCooldown_GeneratesNewOtp_InvalidatesOldOtp()
    {
        _factory.TestSms.Reset();
        var email = $"resend_valid_{Guid.NewGuid()}@assistlk.com";
        var startRequest = new RegisterStartRequest
        {
            FullName = "Patient User",
            Email = email,
            Password = "Password123!",
            PhoneNumber = "0779990011",
            Role = UserRole.Customer
        };

        var startResponse = await PostJsonAsync("/api/auth/register/start", startRequest);
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        var startResult = await startResponse.Content.ReadFromJsonAsync<RegisterStartResponse>(JsonOptions);
        var oldOtp = _factory.TestSms.LastOtp!;

        // Fast forward LastSentAtUtc beyond 45s
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AssistLKDbContext>();
            var challenge = await context.RegistrationChallenges.FindAsync(startResult!.ChallengeId);
            challenge!.LastSentAtUtc = DateTime.UtcNow.AddSeconds(-50);
            await context.SaveChangesAsync();
        }

        // Resend
        var resendResponse = await PostJsonAsync("/api/auth/register/resend-otp", new ResendOtpRequest
        {
            ChallengeId = startResult!.ChallengeId
        });
        Assert.Equal(HttpStatusCode.OK, resendResponse.StatusCode);

        var resendResult = await resendResponse.Content.ReadFromJsonAsync<ResendOtpResponse>(JsonOptions);
        Assert.Equal(45, resendResult!.CooldownSeconds);

        var newOtp = _factory.TestSms.LastOtp!;
        Assert.NotNull(newOtp);

        // Verify with OLD OTP must fail
        var oldVerify = await PostJsonAsync("/api/auth/register/verify-otp", new VerifyOtpRequest
        {
            ChallengeId = startResult.ChallengeId,
            Otp = oldOtp
        });
        Assert.Equal(HttpStatusCode.BadRequest, oldVerify.StatusCode);

        // Verify with NEW OTP must succeed
        var newVerify = await PostJsonAsync("/api/auth/register/verify-otp", new VerifyOtpRequest
        {
            ChallengeId = startResult.ChallengeId,
            Otp = newOtp
        });
        Assert.Equal(HttpStatusCode.Created, newVerify.StatusCode);
    }

    [Fact]
    public async Task ResendOtp_MaxResendsExceeded_Returns400()
    {
        _factory.TestSms.Reset();
        var startRequest = new RegisterStartRequest
        {
            FullName = "Spam Resender",
            Email = $"max_resend_{Guid.NewGuid()}@assistlk.com",
            Password = "Password123!",
            PhoneNumber = "0771010101",
            Role = UserRole.Customer
        };

        var startResponse = await PostJsonAsync("/api/auth/register/start", startRequest);
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        var startResult = await startResponse.Content.ReadFromJsonAsync<RegisterStartResponse>(JsonOptions);

        // Set ResendCount = 3 in DB
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AssistLKDbContext>();
            var challenge = await context.RegistrationChallenges.FindAsync(startResult!.ChallengeId);
            challenge!.ResendCount = 3;
            challenge.LastSentAtUtc = DateTime.UtcNow.AddSeconds(-60);
            await context.SaveChangesAsync();
        }

        var resendResponse = await PostJsonAsync("/api/auth/register/resend-otp", new ResendOtpRequest
        {
            ChallengeId = startResult!.ChallengeId
        });

        Assert.Equal(HttpStatusCode.BadRequest, resendResponse.StatusCode);
        var body = await resendResponse.Content.ReadAsStringAsync();
        Assert.Contains("Maximum resend", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SmsFailure_Returns503_ChallengeRolledBack()
    {
        _factory.TestSms.Reset();
        _factory.TestSms.ShouldFail = true;

        var email = $"sms_fail_{Guid.NewGuid()}@assistlk.com";
        var startRequest = new RegisterStartRequest
        {
            FullName = "Unlucky User",
            Email = email,
            Password = "Password123!",
            PhoneNumber = "0771212121",
            Role = UserRole.Customer
        };

        var response = await PostJsonAsync("/api/auth/register/start", startRequest);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        // Challenge should NOT be orphaned in DB
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AssistLKDbContext>();
        var challenge = await context.RegistrationChallenges.FirstOrDefaultAsync(c => c.Email == email);
        Assert.Null(challenge);

        _factory.TestSms.ShouldFail = false;
    }

    [Fact]
    public async Task LegacyUser_LoginAndJwt_RemainsIntact()
    {
        var email = $"legacy_login_{Guid.NewGuid()}@assistlk.com";
        var password = "LegacyPassword123!";
        var hasher = new PasswordHasher<User>();
        var dummyUser = new User();
        var hash = hasher.HashPassword(dummyUser, password);

        await _factory.SeedAsync(async context =>
        {
            context.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                FullName = "Legacy Customer",
                Email = email,
                PasswordHash = hash,
                PhoneNumber = "077-999-8888", // Legacy unnormalized phone
                Role = UserRole.Customer,
                IsActive = true,
                IsPhoneVerified = false,
                PhoneVerifiedAtUtc = null
            });
            await Task.CompletedTask;
        });

        var loginRequest = new LoginRequest
        {
            Email = email,
            Password = password
        };

        var response = await PostJsonAsync("/api/auth/login", loginRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var authResult = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        Assert.NotNull(authResult);
        Assert.False(string.IsNullOrWhiteSpace(authResult.Token));
        Assert.Equal(email, authResult.Email);
    }
}
