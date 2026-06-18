using EbenezerBackend.Features.Auth.Data.Models;
using EbenezerBackend.Features.Auth.Domain.Entities;
using EbenezerBackend.Features.Auth.Domain.Repositories;
using EbenezerBackend.Shared.Data;
using Microsoft.AspNetCore.Identity;

namespace EbenezerBackend.Features.Auth.Data;

public class AuthRepository : BaseRepository<AuthUserModel>, IAuthRepository, IUserPasswordStore<AuthUserEntity>
{
    // IAuthRepository methods
    public Task<AuthUserEntity> RegisterUserAsync(AuthUserEntity authUser)
        => throw new NotImplementedException();

    public Task<AuthUserEntity?> FindByUserName(string userName)
        => throw new NotImplementedException();

    public Task<bool> UserExistsByUserNameOrEmail(string userName, string email)
        => throw new NotImplementedException();

    // IUserPasswordStore methods
    public Task<string> GetUserIdAsync(AuthUserEntity authUser, CancellationToken ct)
        => Task.FromResult(authUser.Id);

    public Task<string?> GetUserNameAsync(AuthUserEntity authUser, CancellationToken ct)
        => Task.FromResult(authUser.UserName);

    public Task SetUserNameAsync(AuthUserEntity authUser, string? userName, CancellationToken ct)
    {
        authUser.UserName = userName;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedUserNameAsync(AuthUserEntity authUser, CancellationToken ct)
        => Task.FromResult(authUser.NormalizedUserName);

    public Task SetNormalizedUserNameAsync(AuthUserEntity authUser, string? normalizedName, CancellationToken ct)
    {
        authUser.NormalizedUserName = normalizedName;
        return Task.CompletedTask;
    }

    public Task<IdentityResult> CreateAsync(AuthUserEntity authUser, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<IdentityResult> UpdateAsync(AuthUserEntity authUser, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<AuthUserEntity?> FindByIdAsync(string userId, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<AuthUserEntity?> FindByNameAsync(string normalizedUserName, CancellationToken ct)
        => throw new NotImplementedException();

    public Task SetPasswordHashAsync(AuthUserEntity authUser, string? passwordHash, CancellationToken ct)
    {
        authUser.PasswordHash = passwordHash;
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(AuthUserEntity authUser, CancellationToken ct)
        => Task.FromResult(authUser.PasswordHash);

    public Task<bool> HasPasswordAsync(AuthUserEntity authUser, CancellationToken ct)
        => Task.FromResult(!string.IsNullOrEmpty(authUser.PasswordHash));

    public Task<IdentityResult> DeleteAsync(AuthUserEntity authUser, CancellationToken ct)
        => throw new NotImplementedException();

    public void Dispose() { }
}
