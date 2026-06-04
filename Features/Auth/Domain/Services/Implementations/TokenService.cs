using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EbenezerBackend.Features.Auth.Domain.Entities;
using EbenezerBackend.Features.Auth.Domain.Services.Interfaces;
using EbenezerBackend.Shared.Configurations.Interfaces;
using Microsoft.IdentityModel.Tokens;

namespace EbenezerBackend.Features.Auth.Domain.Services.Implementations;

public class TokenService(IVariables variables) : ITokenService
{
    public string GenerateToken(AuthUserEntity authUser)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, authUser.Id),
            new(ClaimTypes.Name, authUser.UserName ?? ""),
            new(ClaimTypes.Email, authUser.Email ?? ""),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(variables.JwtSecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(2),
            SigningCredentials = credentials,
            Issuer = variables.JwtIssuer,
            Audience = variables.JwtAudience,
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }
}