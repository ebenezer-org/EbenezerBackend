using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArangoDBNetStandard;
using EbenezerBackend.Features.Profile.Data.Models;
using EbenezerBackend.Features.Profile.Domain.Entities;
using EbenezerBackend.Features.Profile.Domain.Repositories;
using EbenezerBackend.Shared;
using EbenezerBackend.Shared.Exceptions;

namespace EbenezerBackend.Features.Profile.Data;

public class ProfileRepository(IArangoDBClient db) : BaseRepository<ProfileModel>, IProfileRepository
{
    public async Task<ProfileEntity> RegisterProfileAsync(ProfileEntity profileEntity)
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

        var response = await db.Cursor.PostCursorAsync<ProfileModel>(query, bindVars);

        var responseProfileModel = response.Result.FirstOrDefault();

        if (responseProfileModel is null)
        {
            throw new InternalException("Database didn't return updated Profile");
        }
        
        return responseProfileModel.ToEntity();
    }

    public async Task<ProfileEntity> FindProfileByUserNameAsync(string username)
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

        var response = await db.Cursor.PostCursorAsync<ProfileModel>(query, bindVars);
        
        var profileFound = response.Result.FirstOrDefault();

        return profileFound?.ToEntity() ?? throw new NotFoundException("Usuário não encontrado");
    }
}