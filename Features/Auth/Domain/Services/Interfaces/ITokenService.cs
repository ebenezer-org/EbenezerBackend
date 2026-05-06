using EbenezerBackend.Features.Auth.Domain.Entities;

namespace EbenezerBackend.Features.Auth.Domain.Services.Interfaces;

public interface ITokenService
{
    string GenerateToken(UserEntity user);
}