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

public class AuthRepository(IArangoDBClient db) : BaseRepository<AuthUserModel>, IAuthRepository, IUserPasswordStore<AuthUserEntity>
{
    // IAuthRepository methods
    public async Task<AuthUserEntity> RegisterUserAsync(AuthUserEntity authUser)
    {
        var userModel = AuthUserModel.FromEntity(authUser);
        
        var response = await db.Document.PostDocumentAsync<AuthUserModel>(
            CollectionName, 
            userModel,
            new PostDocumentsQuery { ReturnNew =  true }
        );

        return response.New.ToEntity();
    }

    public async Task<AuthUserEntity?> FindByUserName(string userName)
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

        var response = await db.Cursor.PostCursorAsync<AuthUserModel>(query, bindVars);

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
    public async Task<string> GetUserIdAsync(AuthUserEntity authUser, CancellationToken ct) 
        => await Task.FromResult(authUser.Id);

    public async Task<string?> GetUserNameAsync(AuthUserEntity authUser, CancellationToken ct) 
        => await Task.FromResult(authUser.UserName);

    public Task SetUserNameAsync(AuthUserEntity authUser, string? userName, CancellationToken ct)
    {
        authUser.UserName = userName;
        return Task.CompletedTask;
    }

    public async Task<string?> GetNormalizedUserNameAsync(AuthUserEntity authUser, CancellationToken ct) 
        => await Task.FromResult(authUser.NormalizedUserName);

    public Task SetNormalizedUserNameAsync(AuthUserEntity authUser, string? normalizedName, CancellationToken ct)
    {
        authUser.NormalizedUserName = normalizedName;
        return Task.CompletedTask;
    }

    public async Task<IdentityResult> CreateAsync(AuthUserEntity authUser, CancellationToken ct)
    {
        var userModel = AuthUserModel.FromEntity(authUser);
        
        await db.Document.PostDocumentAsync($"{CollectionName}", userModel, token: ct);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateAsync(AuthUserEntity authUser, CancellationToken ct)
    {
        var userModel = AuthUserModel.FromEntity(authUser);
        
        await db.Document.PutDocumentAsync($"{CollectionName}", userModel.Id, userModel, token: ct);
        return IdentityResult.Success;
    }

    public async Task<AuthUserEntity?> FindByIdAsync(string userId, CancellationToken ct)
    {
        var response = await db.Document.GetDocumentAsync<AuthUserEntity>($"{CollectionName}", userId, token: ct);
        return response;
    }

    public async Task<AuthUserEntity?> FindByNameAsync(string normalizedUserName, CancellationToken ct)
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
        
        var cursor = await db.Cursor.PostCursorAsync<AuthUserEntity>(query, bindVars, token: ct);
    
        return cursor.Result.FirstOrDefault();
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

    public async Task<IdentityResult> DeleteAsync(AuthUserEntity authUser, CancellationToken ct)
    {
        var query = $@"
            FOR u IN {CollectionName}
                FILTER u.UserName == @username
                UPDATE u WITH @user IN {CollectionName}
        ";
        
        var bindVars = new Dictionary<string, object>()
        {
            { "username", authUser.UserName!},
            { "user", authUser },
        };
        
        var cursor = await db.Cursor.PostCursorAsync<AuthUserModel>(query, bindVars, token: ct);
        
        return cursor is null ? IdentityResult.Failed() : IdentityResult.Success;
    }
    
    public void Dispose() {}
}