using EbenezerBackend.Features.Profile.Domain.Repositories;
using EbenezerBackend.Shared.Web.Exceptions;

namespace EbenezerBackend.Shared.Web.Services.EnsureUserExists;

public class EnsureUserExistsService(IProfileRepository repository) : IEnsureUserExistsService
{
    public async Task EnsureExistsByIdAsync(string userId, CancellationToken ct)
    {
        var result = await repository.UserExistsById(userId, ct);

        if (!result)
        {
            throw new ProfileNotFoundException(userId);
        }
    }

    public async Task EnsureExistsByUsernameAsync(string username, CancellationToken ct)
    {
        var result = await repository.UserExistsByUserName(username, ct);

        if (!result)
        {
            throw new ProfileNotFoundException(username);
        }
    }
}