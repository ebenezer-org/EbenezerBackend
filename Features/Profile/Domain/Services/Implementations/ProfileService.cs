using System.Threading.Tasks;
using EbenezerBackend.Features.Profile.Domain.Entities;
using EbenezerBackend.Features.Profile.Domain.Repositories;
using EbenezerBackend.Features.Profile.Domain.Services.Interfaces;
using EbenezerBackend.Features.Profile.Presentation.Dtos.Get;
using EbenezerBackend.Features.Profile.Presentation.Dtos.Register;
using EbenezerBackend.Shared.Services.UserContext;

namespace EbenezerBackend.Features.Profile.Domain.Services.Implementations;

public class ProfileService(IProfileRepository repository, IUserContext userContext) : IProfileService
{
    public async Task<RegisterProfileResponseDto> RegisterProfileAsync(RegisterProfileRequestDto request)
    {
        var entity = new ProfileEntity(userContext.UserName, request.FullName, request.Bio, request.Phone);

        var response = await repository.RegisterProfileAsync(entity);

        return new RegisterProfileResponseDto(response.UserName, response.FullName, response.Bio, response.Phone);
    }

    public async Task<GetProfileResponseDto> GetProfileAsync(string username)
    {
        var response = await repository.FindProfileByUserNameAsync(username);
        
        return new GetProfileResponseDto(response.FullName, response.Bio, response.Phone);
    }
}