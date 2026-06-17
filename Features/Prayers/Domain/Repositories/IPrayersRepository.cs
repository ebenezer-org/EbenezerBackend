using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.Insert;
using EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.List;
using EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.Timeline;

namespace EbenezerBackend.Features.Prayers.Domain.Repositories;

public interface IPrayersRepository
{
    Task<InsertPrayerResponseDto> InsertPrayerAsync(InsertPrayerRequestDto request, CancellationToken ct);
    Task<IReadOnlyCollection<ListPrayerRepositoryResponseDto>> ListPrayersAsync(string? viewerUserName, CancellationToken ct);
    Task<ListPrayerRepositoryResponseDto?> FindPrayerByIdAsync(string prayerId, string? viewerUserName, CancellationToken ct);
    Task<ListPrayerRepositoryResponseDto?> UpdatePrayerAsync(string prayerId, string ownerUserName, string content, bool isPublic,
        IReadOnlyCollection<string> categoryIds, CancellationToken ct);
    Task<bool> DeletePrayerAsync(string prayerId, string ownerUserName, CancellationToken ct);
    Task<ListPrayerRepositoryResponseDto?> SetAuthorResponseAsync(string prayerId, string ownerUserName, string? message,
        CancellationToken ct);
    Task AddSupportReactionAsync(string prayerId, string reactorUserName, CancellationToken ct);
    Task RemoveSupportReactionAsync(string prayerId, string reactorUserName, CancellationToken ct);
    Task<(IReadOnlyCollection<TimelinePrayerRepositoryResponseDto>, int TotalCount)> GetTimelineAsync(string viewerUserName, int page, int pageSize,
        CancellationToken ct);
    Task<IReadOnlyCollection<ListPrayerRepositoryResponseDto>> SearchPrayersAsync(string? viewerUserName, string? authorUserName,
        string? categoryId, string? text, int page, int pageSize, CancellationToken ct);
}
