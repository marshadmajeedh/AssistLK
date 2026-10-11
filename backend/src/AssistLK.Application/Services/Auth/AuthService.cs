using AssistLK.Application.Auth.DTOs;
using AssistLK.Application.Common;
using AssistLK.Application.Common.Exceptions;
using AssistLK.Application.Interfaces;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AssistLK.Application.Services.Auth;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IRegistrationChallengeRepository _challengeRepository;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IOtpSecurityService _otpSecurityService;
    private readonly ISmsService _smsService;

    public AuthService(
        IUserRepository userRepository,
        IRegistrationChallengeRepository challengeRepository,
        IPasswordHasher<User> passwordHasher,
        IJwtTokenService jwtTokenService,
        IOtpSecurityService otpSecurityService,
        ISmsService smsService)
    {
        _userRepository = userRepository;
        _challengeRepository = challengeRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _otpSecurityService = otpSecurityService;
        _smsService = smsService;
    }

    public async Task<AuthResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email
            .Trim()
            .ToLowerInvariant();

        // Admin accounts must not be publicly registered.
        if (request.Role == UserRole.Admin)
        {
            throw new UnauthorizedAccessException(
                "Admin accounts cannot be registered publicly.");
        }

        // Customer registration requires mobile phone OTP verification.
        if (request.Role == UserRole.Customer)
        {
            throw new ArgumentException(
                "Customer registration requires phone verification. Please use the mobile verification flow.");
        }

        if (request.Role != UserRole.Provider)
        {
            throw new ArgumentException(
                "A valid Provider role is required.");
        }

        if (await _userRepository.EmailExistsAsync(
                email,
                cancellationToken))
        {
            throw new ConflictException(
                "An account with this email already exists.");
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PhoneNumber = request.PhoneNumber?.Trim(),
            Role = request.Role,
            IsActive = true,
            IsPhoneVerified = false,
            PhoneVerifiedAtUtc = null
        };

        user.PasswordHash =
            _passwordHasher.HashPassword(
                user,
                request.Password);

        await _userRepository.AddAsync(
            user,
            cancellationToken);

        await _userRepository.SaveChangesAsync(
            cancellationToken);

        return CreateAuthResponse(user);
    }

    public async Task<RegisterStartResponse> RegisterStartAsync(
        RegisterStartRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Role != UserRole.Customer)
        {
            throw new ArgumentException(
                "Only Customer registration is supported by this endpoint.");
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new ArgumentException("Full name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new ArgumentException("Email is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            throw new ArgumentException("Password must contain at least 8 characters.");
        }

        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            throw new ArgumentException("Phone number is required.");
        }

        if (!PhoneNumberNormalizer.TryNormalizeSriLankanMobile(request.PhoneNumber, out var normalizedPhone))
        {
            throw new ArgumentException("Please enter a valid Sri Lankan mobile phone number (e.g. 0771234567 or +94771234567).");
        }

        var email = request.Email.Trim().ToLowerInvariant();

        if (await _userRepository.EmailExistsAsync(email, cancellationToken))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        if (await _userRepository.VerifiedCustomerPhoneExistsAsync(normalizedPhone, cancellationToken))
        {
            throw new ConflictException("An account with this phone number already exists.");
        }

        var challengeId = Guid.NewGuid();
        var otp = _otpSecurityService.GenerateOtp();
        var otpHash = _otpSecurityService.ComputeOtpHash(challengeId, otp);

        var tempUser = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PhoneNumber = normalizedPhone,
            Role = UserRole.Customer,
            IsActive = true
        };
        var passwordHash = _passwordHasher.HashPassword(tempUser, request.Password);

        var challenge = new RegistrationChallenge
        {
            Id = challengeId,
            FullName = request.FullName.Trim(),
            Email = email,
            PhoneNumber = normalizedPhone,
            PasswordHash = passwordHash,
            Role = UserRole.Customer,
            OtpHash = otpHash,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
            AttemptCount = 0,
            MaxAttempts = 5,
            ResendCount = 0,
            LastSentAtUtc = DateTime.UtcNow,
            IsConsumed = false
        };

        await _challengeRepository.AddAsync(challenge, cancellationToken);
        await _challengeRepository.SaveChangesAsync(cancellationToken);

        // Transaction safety: If SMS dispatch fails, remove the challenge and fail safely with 503
        try
        {
            await _smsService.SendOtpAsync(normalizedPhone, otp, cancellationToken);
        }
        catch (Exception ex)
        {
            try
            {
                await _challengeRepository.RemoveAsync(challenge, cancellationToken);
                await _challengeRepository.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                // Suppress rollback cleanup exception
            }

            throw new ServiceUnavailableException("Unable to deliver verification code. Please try again later.", ex);
        }

        return new RegisterStartResponse
        {
            ChallengeId = challenge.Id,
            MaskedPhoneNumber = PhoneNumberNormalizer.Mask(normalizedPhone),
            ExpiresAtUtc = challenge.ExpiresAtUtc,
            CooldownSeconds = 45
        };
    }

    public async Task<AuthResponse> VerifyOtpAsync(
        VerifyOtpRequest request,
        CancellationToken cancellationToken = default)
    {
        var challenge = await _challengeRepository.GetByIdAsync(request.ChallengeId, cancellationToken);
        if (challenge is null || challenge.Role != UserRole.Customer)
        {
            throw new KeyNotFoundException("Verification challenge was not found.");
        }

        if (challenge.IsConsumed)
        {
            throw new ArgumentException("This verification challenge has already been completed.");
        }

        if (challenge.ExpiresAtUtc < DateTime.UtcNow)
        {
            throw new ArgumentException("Verification code has expired. Please request a new code.");
        }

        if (challenge.AttemptCount >= challenge.MaxAttempts)
        {
            throw new ArgumentException("Maximum verification attempts exceeded. Please restart registration.");
        }

        var isOtpValid = _otpSecurityService.VerifyOtp(challenge.Id, request.Otp, challenge.OtpHash);
        if (!isOtpValid)
        {
            challenge.AttemptCount++;
            await _challengeRepository.SaveChangesAsync(cancellationToken);

            var remaining = challenge.MaxAttempts - challenge.AttemptCount;
            if (remaining <= 0)
            {
                throw new ArgumentException("Maximum verification attempts exceeded. Please restart registration.");
            }

            throw new ArgumentException($"Invalid verification code. {remaining} attempt{(remaining == 1 ? "" : "s")} remaining.");
        }

        // Re-check uniqueness before user creation
        if (await _userRepository.EmailExistsAsync(challenge.Email, cancellationToken))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        if (await _userRepository.VerifiedCustomerPhoneExistsAsync(challenge.PhoneNumber, cancellationToken))
        {
            throw new ConflictException("An account with this phone number already exists.");
        }

        var user = new User
        {
            FullName = challenge.FullName,
            Email = challenge.Email,
            PhoneNumber = challenge.PhoneNumber,
            PasswordHash = challenge.PasswordHash,
            Role = challenge.Role,
            IsActive = true,
            IsPhoneVerified = true,
            PhoneVerifiedAtUtc = DateTime.UtcNow
        };

        challenge.IsConsumed = true;

        try
        {
            await _userRepository.AddAsync(user, cancellationToken);
            await _challengeRepository.SaveChangesAsync(cancellationToken);
            await _userRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            throw new ConflictException("An account with this phone number or email already exists.", ex);
        }

        return CreateAuthResponse(user);
    }

    public async Task<ResendOtpResponse> ResendOtpAsync(
        ResendOtpRequest request,
        CancellationToken cancellationToken = default)
    {
        var challenge = await _challengeRepository.GetByIdAsync(request.ChallengeId, cancellationToken);
        if (challenge is null || challenge.Role != UserRole.Customer)
        {
            throw new KeyNotFoundException("Verification challenge was not found.");
        }

        if (challenge.IsConsumed)
        {
            throw new ArgumentException("This verification challenge has already been completed.");
        }

        if (challenge.ResendCount >= 3)
        {
            throw new ArgumentException("Maximum resend attempts reached. Please restart registration.");
        }

        var elapsedSeconds = (DateTime.UtcNow - challenge.LastSentAtUtc).TotalSeconds;
        if (elapsedSeconds < 45)
        {
            var waitRemaining = (int)Math.Ceiling(45 - elapsedSeconds);
            throw new ArgumentException($"Please wait {waitRemaining} second{(waitRemaining == 1 ? "" : "s")} before requesting another code.");
        }

        var newOtp = _otpSecurityService.GenerateOtp();
        challenge.OtpHash = _otpSecurityService.ComputeOtpHash(challenge.Id, newOtp);
        challenge.LastSentAtUtc = DateTime.UtcNow;
        challenge.ResendCount++;
        challenge.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5);

        await _challengeRepository.SaveChangesAsync(cancellationToken);

        try
        {
            await _smsService.SendOtpAsync(challenge.PhoneNumber, newOtp, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new ServiceUnavailableException("Unable to deliver verification code. Please try again later.", ex);
        }

        return new ResendOtpResponse
        {
            CooldownSeconds = 45,
            ExpiresAtUtc = challenge.ExpiresAtUtc
        };
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email
            .Trim()
            .ToLowerInvariant();

        var user =
            await _userRepository.GetByEmailAsync(
                email,
                cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedAccessException(
                "Invalid email or password.");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException(
                "This account is inactive.");
        }

        var verification =
            _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                request.Password);

        if (verification ==
            PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedAccessException(
                "Invalid email or password.");
        }

        if (verification ==
            PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash =
                _passwordHasher.HashPassword(
                    user,
                    request.Password);

            await _userRepository.SaveChangesAsync(
                cancellationToken);
        }

        return CreateAuthResponse(user);
    }

    public async Task<UserProfileResponse> GetProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user =
            await _userRepository.GetByIdAsync(
                userId,
                cancellationToken);

        if (user is null)
        {
            throw new KeyNotFoundException(
                "User was not found.");
        }

        return new UserProfileResponse
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Role = user.Role.ToString(),
            IsActive = user.IsActive
        };
    }

    private AuthResponse CreateAuthResponse(User user)
    {
        var jwt =
            _jwtTokenService.GenerateToken(user);

        return new AuthResponse
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role.ToString(),
            Token = jwt.Token,
            ExpiresAtUtc = jwt.ExpiresAtUtc
        };
    }
}