using EbenezerBackend.Features.Profile.Data.Models;
using EbenezerBackend.Features.Profile.Domain.Entities;
using EbenezerBackend.Features.Profile.Domain.Repositories;
using EbenezerBackend.Shared.Data;

namespace EbenezerBackend.Features.Profile.Data;

public class ProfileRepository : BaseRepository<ProfileModel>, IProfileRepository
{
    public Task<ProfileEntity> RegisterProfileAsync(ProfileEntity profileEntity, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<ProfileEntity?> FindProfileByUserNameAsync(string username, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<bool> UserExistsById(string userId, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<bool> UserExistsByUserName(string username, CancellationToken ct)
        => throw new NotImplementedException();
}
