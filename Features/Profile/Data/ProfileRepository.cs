using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArangoDBNetStandard;
using EbenezerBackend.Features.Profile.Data.Models;
using EbenezerBackend.Features.Profile.Domain.Entities;
using EbenezerBackend.Features.Profile.Domain.Repositories;
using EbenezerBackend.Infrastructure.Extensions.Web;
using EbenezerBackend.Shared;
using EbenezerBackend.Shared.Data;
using EbenezerBackend.Shared.Web.Exceptions;

namespace EbenezerBackend.Features.Profile.Data;

public class ProfileRepository(IArangoDBClient db) : BaseRepository<ProfileModel>, IProfileRepository
{
    public async Task<ProfileEntity> RegisterProfileAsync(ProfileEntity profileEntity, CancellationToken ct)
    {
        var profileModel = ProfileModel.FromEntity(profileEntity);

        var query = $@"
            FOR p IN {CollectionName}
                FILTER p.UserName == @username
                UPDATE p WITH @profileData IN {CollectionName}
                RETURN NEW
        ";
        
        var bindVars = new Dictionary<string, object>
        {
            { "username", profileModel.UserName },
            { "profileData", profileModel }
        };

        var response = await db.Cursor.PostCursorAsync<ProfileModel>(query, bindVars, token: ct);

        var responseProfileModel = response.Result.FirstOrDefault();

        if (responseProfileModel is null)
        {
            throw new InternalException("Database didn't return updated Profile");
        }
        
        return responseProfileModel.ToEntity();
    }

    public async Task<ProfileEntity?> FindProfileByUserNameAsync(string username, CancellationToken ct)
    {
        var query = $@"
            FOR p IN {CollectionName}
                FILTER p.UserName == @username
                RETURN p
        ";

        var bindVars = new Dictionary<string, object>()
        {
            { "username", username }
        };

        var response = await db.Cursor.PostCursorAsync<ProfileModel>(query, bindVars, token: ct);
        
        var profileFound = response.Result.FirstOrDefault();

        return profileFound?.ToEntity();
    }

    public async Task<bool> UserExistsById(string userId, CancellationToken ct)
    {
        var response =
            await db.Document.HeadDocumentAsync(ArangoDbUtils.BuildArangoDbId(userId, CollectionName), token: ct);
        
        return response.Code.IsSuccess();
    }

    public async Task<bool> UserExistsByUserName(string username, CancellationToken ct)
    {
        var query = $@"FOR u IN {CollectionName} FILTER u.UserName == @username LIMIT 1 RETURN 1";

        var bindVars = new Dictionary<string, object> { { "username", username } };
        
        return await db.Cursor.PostCursorAsync<int>(query, bindVars, token: ct)
            .ContinueWith(t => t.Result.Result.Any(), ct);
    }
}