using AssistLK.Application.Auth.DTOs;
using AssistLK.Domain.Entities;

namespace AssistLK.Application.Interfaces;

public interface IJwtTokenService
{
    JwtTokenResult GenerateToken(User user);
}