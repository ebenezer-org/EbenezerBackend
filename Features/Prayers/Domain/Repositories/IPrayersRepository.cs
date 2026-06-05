using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.Insert;
using EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.List;

namespace EbenezerBackend.Features.Prayers.Domain.Repositories;

public interface IPrayersRepository
{
    public Task<InsertPrayerResponseDto> InsertPrayerAsync(InsertPrayerRequestDto request, CancellationToken ct);
    public Task<IReadOnlyCollection<ListPrayerRepositoryResponseDto>> ListPrayersAsync(CancellationToken ct);
}
