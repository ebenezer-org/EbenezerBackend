using System.Threading.Tasks;
using EbenezerBackend.Features.Profile.Domain.Entities;

namespace EbenezerBackend.Features.Profile.Domain.Repositories;

public interface IProfileRepository
{
    public Task<ProfileEntity> RegisterProfileAsync(ProfileEntity profileEntity, CancellationToken ct);
    public Task<ProfileEntity?> FindProfileByUserNameAsync(string username, CancellationToken ct);
    public Task<bool> UserExistsById(string userId, CancellationToken ct);
    public Task<bool> UserExistsByUserName(string username, CancellationToken ct);
}