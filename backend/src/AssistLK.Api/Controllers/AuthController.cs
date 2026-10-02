using System.Security.Claims;
using AssistLK.Application.Auth.DTOs;
using AssistLK.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AssistLK.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(
        IAuthService authService)
    {
        _authService = authService;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>>
        Register(
            RegisterRequest request,
            CancellationToken cancellationToken)
    {
        var response =
            await _authService.RegisterAsync(
                request,
                cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            response);
    }

    [AllowAnonymous]
    [EnableRateLimiting("OtpPolicy")]
    [HttpPost("register/start")]
    public async Task<ActionResult<RegisterStartResponse>>
        RegisterStart(
            RegisterStartRequest request,
            CancellationToken cancellationToken)
    {
        var response =
            await _authService.RegisterStartAsync(
                request,
                cancellationToken);

        return Ok(response);
    }

    [AllowAnonymous]
    [EnableRateLimiting("OtpPolicy")]
    [HttpPost("register/verify-otp")]
    public async Task<ActionResult<AuthResponse>>
        VerifyOtp(
            VerifyOtpRequest request,
            CancellationToken cancellationToken)
    {
        var response =
            await _authService.VerifyOtpAsync(
                request,
                cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            response);
    }

    [AllowAnonymous]
    [EnableRateLimiting("OtpPolicy")]
    [HttpPost("register/resend-otp")]
    public async Task<ActionResult<ResendOtpResponse>>
        ResendOtp(
            ResendOtpRequest request,
            CancellationToken cancellationToken)
    {
        var response =
            await _authService.ResendOtpAsync(
                request,
                cancellationToken);

        return Ok(response);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>>
        Login(
            LoginRequest request,
            CancellationToken cancellationToken)
    {
        var response =
            await _authService.LoginAsync(
                request,
                cancellationToken);

        return Ok(response);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserProfileResponse>>
        Me(CancellationToken cancellationToken)
    {
        var idValue =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(
                idValue,
                out var userId))
        {
            return Unauthorized();
        }

        var response =
            await _authService.GetProfileAsync(
                userId,
                cancellationToken);

        return Ok(response);
    }
}