using EbenezerBackend.Features.Auth.Data.Models;
using EbenezerBackend.Features.Auth.Domain.Entities;
using EbenezerBackend.Features.Auth.Domain.Repositories;
using EbenezerBackend.Shared.Data;
using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;

namespace EbenezerBackend.Features.Auth.Data;

public class AuthRepository(IMongoDatabase database) : BaseRepository<AuthUserModel>, IAuthRepository, IUserPasswordStore<AuthUserEntity>
{
    private readonly IMongoCollection<AuthUserModel> _collection = database.GetCollection<AuthUserModel>(CollectionName);

    // ── IAuthRepository ────────────────────────────────────────────────────────

    public async Task<AuthUserEntity> RegisterUserAsync(AuthUserEntity authUser)
    {
        var model = AuthUserModel.FromEntity(authUser);
        await _collection.InsertOneAsync(model);
        return authUser;
    }

    public async Task<AuthUserEntity?> FindByUserName(string userName)
    {
        var filter = Builders<AuthUserModel>.Filter.Eq(x => x.UserName, userName);
        var model = await _collection.Find(filter).FirstOrDefaultAsync();
        return model?.ToEntity();
    }

    public async Task<bool> UserExistsByUserNameOrEmail(string userName, string email)
    {
        var filter = Builders<AuthUserModel>.Filter.Or(
            Builders<AuthUserModel>.Filter.Eq(x => x.UserName, userName),
            Builders<AuthUserModel>.Filter.Eq(x => x.Email, email)
        );
        return await _collection.Find(filter).AnyAsync();
    }

    // ── IUserPasswordStore ─────────────────────────────────────────────────────

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

    public async Task<IdentityResult> CreateAsync(AuthUserEntity authUser, CancellationToken ct)
    {
        try
        {
            var model = AuthUserModel.FromEntity(authUser);
            await _collection.InsertOneAsync(model, cancellationToken: ct);
            return IdentityResult.Success;
        }
        catch (Exception ex)
        {
            return IdentityResult.Failed(new IdentityError { Description = ex.Message });
        }
    }

    public async Task<IdentityResult> UpdateAsync(AuthUserEntity authUser, CancellationToken ct)
    {
        try
        {
            var filter = Builders<AuthUserModel>.Filter.Eq(x => x.Id, authUser.Id);
            var model = AuthUserModel.FromEntity(authUser);
            await _collection.ReplaceOneAsync(filter, model, cancellationToken: ct);
            return IdentityResult.Success;
        }
        catch (Exception ex)
        {
            return IdentityResult.Failed(new IdentityError { Description = ex.Message });
        }
    }

    public async Task<IdentityResult> DeleteAsync(AuthUserEntity authUser, CancellationToken ct)
    {
        try
        {
            var filter = Builders<AuthUserModel>.Filter.Eq(x => x.Id, authUser.Id);
            await _collection.DeleteOneAsync(filter, cancellationToken: ct);
            return IdentityResult.Success;
        }
        catch (Exception ex)
        {
            return IdentityResult.Failed(new IdentityError { Description = ex.Message });
        }
    }

    public async Task<AuthUserEntity?> FindByIdAsync(string userId, CancellationToken ct)
    {
        var filter = Builders<AuthUserModel>.Filter.Eq(x => x.Id, userId);
        var model = await _collection.Find(filter).FirstOrDefaultAsync(ct);
        return model?.ToEntity();
    }

    public async Task<AuthUserEntity?> FindByNameAsync(string normalizedUserName, CancellationToken ct)
    {
        var filter = Builders<AuthUserModel>.Filter.Eq(x => x.NormalizedUserName, normalizedUserName);
        var model = await _collection.Find(filter).FirstOrDefaultAsync(ct);
        return model?.ToEntity();
    }

    public Task SetPasswordHashAsync(AuthUserEntity authUser, string? passwordHash, CancellationToken ct)
    {
        authUser.PasswordHash = passwordHash;
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(AuthUserEntity authUser, CancellationToken ct)
        => Task.FromResult(authUser.PasswordHash);

    public Task<bool> HasPasswordAsync(AuthUserEntity authUser, CancellationToken ct)
        => Task.FromResult(!string.IsNullOrEmpty(authUser.PasswordHash));

    public void Dispose() { }
}
