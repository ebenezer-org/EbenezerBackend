using EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.Insert;

namespace EbenezerBackend.Features.Prayers.Domain.Repositories;

public interface IPrayersRepository
{
    public Task<InsertPrayerResponseDto> InsertPrayerAsync(InsertPrayerRequestDto request, CancellationToken ct);
}