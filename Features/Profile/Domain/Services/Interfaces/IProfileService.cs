using EbenezerBackend.Features.Profile.Presentation.Dtos.Get;
using EbenezerBackend.Features.Profile.Presentation.Dtos.Register;

namespace EbenezerBackend.Features.Profile.Domain.Services.Interfaces;

public interface IProfileService
{
    public Task<RegisterProfileResponseDto> RegisterProfileAsync(RegisterProfileRequestDto request);
    public Task<GetProfileResponseDto> GetProfileAsync(string username);
}