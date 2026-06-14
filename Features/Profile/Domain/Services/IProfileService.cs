using EbenezerBackend.Features.Profile.Presentation.Dtos.Get;
using EbenezerBackend.Features.Profile.Presentation.Dtos.Register;

namespace EbenezerBackend.Features.Profile.Domain.Services;

public interface IProfileService
{
    public Task<RegisterProfileResponseDto> RegisterProfileAsync(RegisterProfileRequestDto request, CancellationToken ct);
    public Task<GetProfileResponseDto> GetProfileAsync(string username, CancellationToken ct);
}