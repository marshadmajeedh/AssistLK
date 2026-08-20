using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AssistLK.Application.Auth.DTOs;
using AssistLK.Application.Interfaces;
using AssistLK.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace AssistLK.Api.Authentication;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(
        IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public JwtTokenResult GenerateToken(User user)
    {
        var key =
            _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "JWT key is not configured.");

        var issuer =
            _configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException(
                "JWT issuer is not configured.");

        var audience =
            _configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException(
                "JWT audience is not configured.");

        var expirationMinutes =
            _configuration.GetValue<int>(
                "Jwt:ExpirationMinutes");

        if (expirationMinutes <= 0)
        {
            expirationMinutes = 60;
        }

        var expiresAtUtc =
            DateTime.UtcNow.AddMinutes(
                expirationMinutes);

        var claims = new List<Claim>
        {
            new(
                ClaimTypes.NameIdentifier,
                user.Id.ToString()),

            new(
                ClaimTypes.Name,
                user.FullName),

            new(
                ClaimTypes.Email,
                user.Email),

            new(
                ClaimTypes.Role,
                user.Role.ToString())
        };

        var signingKey =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(key));

        var credentials =
            new SigningCredentials(
                signingKey,
                SecurityAlgorithms.HmacSha256);

        var token =
            new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expiresAtUtc,
                signingCredentials: credentials);

        return new JwtTokenResult
        {
            Token =
                new JwtSecurityTokenHandler()
                    .WriteToken(token),

            ExpiresAtUtc = expiresAtUtc
        };
    }
}