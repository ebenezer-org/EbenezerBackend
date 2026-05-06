using EbenezerBackend.Features.Auth.Domain.Entities;
using EbenezerBackend.Features.Auth.Domain.Exceptions;
using EbenezerBackend.Features.Auth.Domain.Repositories.Interfaces;
using EbenezerBackend.Features.Auth.Domain.Services.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace EbenezerBackend.Features.Auth.Domain.Services.Implementations;

public class AuthService(
    IAuthRepository repository,
    IPasswordHasher<UserEntity> hasher,
    ITokenService tokenService) : IAuthService
{
    public async Task<string> RegisterAsync(string userName, string fullName, string email, string password)
    {
        var userExists = await repository.UserExistsByUserNameOrEmail(userName, email);

        if (userExists) throw new UserNameOrEmailAlreadyRegisteredException();
        
        var userEntity = new UserEntity()
        {
            CreatedAt = DateTime.UtcNow,
            FullName = fullName,
            Email = email,
            PasswordHash = password,
            UserName = userName
        };
        
        var hashedPassword = hasher.HashPassword(userEntity, password);

        userEntity.PasswordHash = hashedPassword;
        
        var user = await repository.RegisterUserAsync(userEntity);

        var token = tokenService.GenerateToken(user);

        return token;
    }
    
    public async Task<string> LoginAsync(string userName, string password)
    {
        var userEntity = await repository.FindByUserName(userName);

        if (userEntity == null) 
            throw new LoginFailedException();

        var verificationResult = hasher.VerifyHashedPassword(userEntity, userEntity.PasswordHash!, password);
    
        if (verificationResult == PasswordVerificationResult.Failed)
            throw new LoginFailedException();

        return tokenService.GenerateToken(userEntity);
    }

}