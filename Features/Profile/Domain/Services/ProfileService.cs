using EbenezerBackend.Features.Profile.Domain.Entities;
using EbenezerBackend.Features.Profile.Domain.Repositories;
using EbenezerBackend.Features.Profile.Presentation.Dtos.Get;
using EbenezerBackend.Features.Profile.Presentation.Dtos.Register;
using EbenezerBackend.Shared.Web.Exceptions;
using EbenezerBackend.Shared.Web.Services.EnsureUserExists;
using EbenezerBackend.Shared.Web.Services.UserContext;

namespace EbenezerBackend.Features.Profile.Domain.Services;

public class ProfileService(
    IProfileRepository repository,
    IEnsureUserExistsService ensureUserExistsService,
    IUserContext userContext
    ) : IProfileService
{
    public async Task<RegisterProfileResponseDto> RegisterProfileAsync(RegisterProfileRequestDto request, CancellationToken ct)
    {
        var entity = new ProfileEntity(userContext.UserName, request.FullName, request.Bio, request.Phone);

        var response = await repository.RegisterProfileAsync(entity, ct);

        return new RegisterProfileResponseDto(response.UserName, response.FullName, response.Bio, response.Phone);
    }

    public async Task<GetProfileResponseDto> GetProfileAsync(string username, CancellationToken ct)
    {
        var response = await repository.FindProfileByUserNameAsync(username, ct);

        await ensureUserExistsService.EnsureExistsByUsernameAsync(username, ct);
        
        return new GetProfileResponseDto(response!.Id!, response.UserName, response.FullName, response.Bio, response.Phone);
    }
}