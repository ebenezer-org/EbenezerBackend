using EbenezerBackend.Features.Prayers.Presentation.Dtos.Create;

namespace EbenezerBackend.Features.Prayers.Domain.Services;

public interface IPrayersService
{
    Task<CreatePrayerResponseDto> CreatePostAsync(CreatePrayerRequestDto requesta, CancellationToken ct);
}