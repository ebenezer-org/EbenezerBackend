using EbenezerBackend.Features.Profile.Domain.Entities;

namespace EbenezerBackend.Features.Profile.Domain.Repositories;

public interface IProfileRepository
{
    public Task<ProfileEntity> RegisterProfileAsync(ProfileEntity profileEntity);
    public Task<ProfileEntity> FindProfileByUserNameAsync(string username);
}