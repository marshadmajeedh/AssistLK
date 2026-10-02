using AssistLK.Application.Auth.DTOs;

namespace AssistLK.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default);

    Task<RegisterStartResponse> RegisterStartAsync(
        RegisterStartRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthResponse> VerifyOtpAsync(
        VerifyOtpRequest request,
        CancellationToken cancellationToken = default);

    Task<ResendOtpResponse> ResendOtpAsync(
        ResendOtpRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

    Task<UserProfileResponse> GetProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}