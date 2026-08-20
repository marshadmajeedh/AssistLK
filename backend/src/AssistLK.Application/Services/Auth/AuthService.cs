using AssistLK.Application.Auth.DTOs;
using AssistLK.Application.Common.Exceptions;
using AssistLK.Application.Interfaces;
using AssistLK.Domain.Entities;
using AssistLK.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace AssistLK.Application.Services.Auth;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher<User> passwordHasher,
        IJwtTokenService jwtTokenService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
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

        if (request.Role != UserRole.Customer &&
            request.Role != UserRole.Provider)
        {
            throw new ArgumentException(
                "A valid Customer or Provider role is required.");
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
            IsActive = true
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