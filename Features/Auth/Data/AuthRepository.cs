using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArangoDBNetStandard;
using ArangoDBNetStandard.DocumentApi.Models;
using EbenezerBackend.Features.Auth.Data.Models;
using EbenezerBackend.Features.Auth.Domain.Entities;
using EbenezerBackend.Features.Auth.Domain.Repositories.Interfaces;
using EbenezerBackend.Shared;
using Microsoft.AspNetCore.Identity;

namespace EbenezerBackend.Features.Auth.Data;

public class AuthRepository(IArangoDBClient db) : BaseRepository<UserModel>, IAuthRepository, IUserPasswordStore<UserEntity>
{
    // IAuthRepository methods
    public async Task<UserEntity> RegisterUserAsync(UserEntity user)
    {
        var userModel = UserModel.FromEntity(user);
        
        var response = await db.Document.PostDocumentAsync<UserModel>(
            CollectionName, 
            userModel,
            new PostDocumentsQuery { ReturnNew =  true }
        );

        return response.New.ToEntity();
    }

    public async Task<UserEntity?> FindByUserName(string userName)
    {
        var query = 
            $@"
                FOR u IN {CollectionName}
                    FILTER u.UserName == @username
                    LIMIT 1
                    RETURN u
            ";

        var bindVars = new Dictionary<string, object>
        {
            { "username", userName }
        };

        var response = await db.Cursor.PostCursorAsync<UserModel>(query, bindVars);

        var userModel = response.Result.FirstOrDefault();

        return userModel?.ToEntity();
    }

    public async Task<bool> UserExistsByUserNameOrEmail(string userName, string email)
    {
        var query =
            $@"
                FOR u IN {CollectionName}
                    FILTER u.UserName == @username || u.Email == @email
                    LIMIT 1
                    RETURN 1
            ";

        var bindVars = new Dictionary<string, object>()
        {
            { "username", userName },
            { "email", email },
        };
        
        var cursor = await db.Cursor.PostCursorAsync<int>(query, bindVars);

        return cursor.Result.Any();
    }
    
    // IUserPassword methods
    public async Task<string> GetUserIdAsync(UserEntity user, CancellationToken ct) 
        => await Task.FromResult(user.Id);

    public async Task<string?> GetUserNameAsync(UserEntity user, CancellationToken ct) 
        => await Task.FromResult(user.UserName);

    public Task SetUserNameAsync(UserEntity user, string? userName, CancellationToken ct)
    {
        user.UserName = userName;
        return Task.CompletedTask;
    }

    public async Task<string?> GetNormalizedUserNameAsync(UserEntity user, CancellationToken ct) 
        => await Task.FromResult(user.NormalizedUserName);

    public Task SetNormalizedUserNameAsync(UserEntity user, string? normalizedName, CancellationToken ct)
    {
        user.NormalizedUserName = normalizedName;
        return Task.CompletedTask;
    }

    public async Task<IdentityResult> CreateAsync(UserEntity user, CancellationToken ct)
    {
        var userModel = UserModel.FromEntity(user);
        
        await db.Document.PostDocumentAsync($"{CollectionName}", userModel, token: ct);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateAsync(UserEntity user, CancellationToken ct)
    {
        var userModel = UserModel.FromEntity(user);
        
        await db.Document.PutDocumentAsync($"{CollectionName}", userModel.Id, userModel, token: ct);
        return IdentityResult.Success;
    }

    public async Task<UserEntity?> FindByIdAsync(string userId, CancellationToken ct)
    {
        var response = await db.Document.GetDocumentAsync<UserEntity>($"{CollectionName}", userId, token: ct);
        return response;
    }

    public async Task<UserEntity?> FindByNameAsync(string normalizedUserName, CancellationToken ct)
    {
        var query = $@"
            FOR u IN {CollectionName}
                FILTER u.NormalizedUserName == @username
                LIMIT 1
                RETURN u
        ";

        var bindVars = new Dictionary<string, object>()
        {
            { "username", normalizedUserName },
        };
        
        var cursor = await db.Cursor.PostCursorAsync<UserEntity>(query, bindVars, token: ct);
    
        return cursor.Result.FirstOrDefault();
    }

    public Task SetPasswordHashAsync(UserEntity user, string? passwordHash, CancellationToken ct)
    {
        user.PasswordHash = passwordHash;
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(UserEntity user, CancellationToken ct) 
        => Task.FromResult(user.PasswordHash);

    public Task<bool> HasPasswordAsync(UserEntity user, CancellationToken ct) 
        => Task.FromResult(!string.IsNullOrEmpty(user.PasswordHash));

    public async Task<IdentityResult> DeleteAsync(UserEntity user, CancellationToken ct)
    {
        var query = $@"
            FOR u IN {CollectionName}
                FILTER u.UserName == @username
                UPDATE u WITH @user IN {CollectionName}
        ";
        
        var bindVars = new Dictionary<string, object>()
        {
            { "username", user.UserName!},
            { "user", user },
        };
        
        var cursor = await db.Cursor.PostCursorAsync<UserModel>(query, bindVars, token:ct);
        
        return cursor is null ? IdentityResult.Failed() : IdentityResult.Success;
    }
    
    public void Dispose() {}
}