using EbenezerBackend.Features.Prayers.Presentation.Dtos.Create;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.List;

namespace EbenezerBackend.Features.Prayers.Domain.Services;

public interface IPrayersService
{
    Task<CreatePrayerResponseDto> CreatePostAsync(CreatePrayerRequestDto request, CancellationToken ct);
    Task<IReadOnlyCollection<ListPrayerResponseDto>> ListPrayersAsync(CancellationToken ct);
}
