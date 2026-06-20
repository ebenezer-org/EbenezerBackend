using EbenezerBackend.Features.Profile.Data.Models;
using EbenezerBackend.Features.Profile.Domain.Entities;
using EbenezerBackend.Features.Profile.Domain.Repositories;
using EbenezerBackend.Shared.Data;
using MongoDB.Driver;

namespace EbenezerBackend.Features.Profile.Data;

public class ProfileRepository(IMongoDatabase database) : BaseRepository<ProfileModel>, IProfileRepository
{
    private readonly IMongoCollection<ProfileModel> _collection = database.GetCollection<ProfileModel>(CollectionName);

    public async Task<ProfileEntity> RegisterProfileAsync(ProfileEntity profileEntity, CancellationToken ct)
    {
        var filter = Builders<ProfileModel>.Filter.Eq(x => x.UserName, profileEntity.UserName);

        var update = Builders<ProfileModel>.Update
            .Set(x => x.FullName, profileEntity.FullName)
            .Set(x => x.Bio, profileEntity.Bio)
            .Set(x => x.Phone, profileEntity.Phone);

        var options = new FindOneAndUpdateOptions<ProfileModel>
        {
            ReturnDocument = ReturnDocument.After,
            IsUpsert = false
        };

        var model = await _collection.FindOneAndUpdateAsync(filter, update, options, ct);
        return model is null ? profileEntity : model.ToEntity();
    }

    public async Task<ProfileEntity?> FindProfileByUserNameAsync(string username, CancellationToken ct)
    {
        var filter = Builders<ProfileModel>.Filter.Eq(x => x.UserName, username);
        var model = await _collection.Find(filter).FirstOrDefaultAsync(ct);
        return model?.ToEntity();
    }

    public async Task<bool> UserExistsById(string userId, CancellationToken ct)
    {
        var filter = Builders<ProfileModel>.Filter.Eq(x => x.Id, userId);
        return await _collection.Find(filter).AnyAsync(ct);
    }

    public async Task<bool> UserExistsByUserName(string username, CancellationToken ct)
    {
        var filter = Builders<ProfileModel>.Filter.Eq(x => x.UserName, username);
        return await _collection.Find(filter).AnyAsync(ct);
    }
}
