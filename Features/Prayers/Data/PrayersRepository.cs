using EbenezerBackend.Features.Prayers.Data.Models;
using EbenezerBackend.Features.Prayers.Domain.Repositories;
using EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.Insert;
using EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.List;
using EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.Timeline;
using EbenezerBackend.Shared.Data;

namespace EbenezerBackend.Features.Prayers.Data;

public class PrayersRepository : BaseRepository<PrayerModel>, IPrayersRepository
{
    public Task<InsertPrayerResponseDto> InsertPrayerAsync(InsertPrayerRequestDto request, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<IReadOnlyCollection<ListPrayerRepositoryResponseDto>> ListPrayersAsync(string? viewerUserName, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<ListPrayerRepositoryResponseDto?> FindPrayerByIdAsync(string prayerId, string? viewerUserName, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<ListPrayerRepositoryResponseDto?> UpdatePrayerAsync(
        string prayerId, string ownerUserName, string content, bool isPublic,
        IReadOnlyCollection<string> categoryIds, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<bool> DeletePrayerAsync(string prayerId, string ownerUserName, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<ListPrayerRepositoryResponseDto?> SetAuthorResponseAsync(
        string prayerId, string ownerUserName, string? message, CancellationToken ct)
        => throw new NotImplementedException();

    public Task AddSupportReactionAsync(string prayerId, string reactorUserName, CancellationToken ct)
        => throw new NotImplementedException();

    public Task RemoveSupportReactionAsync(string prayerId, string reactorUserName, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<(IReadOnlyCollection<TimelinePrayerRepositoryResponseDto>, int TotalCount)> GetTimelineAsync(
        string viewerUserName, int page, int pageSize, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<IReadOnlyCollection<ListPrayerRepositoryResponseDto>> SearchPrayersAsync(
        string? viewerUserName, string? authorUserName, string? categoryId, string? text,
        int page, int pageSize, CancellationToken ct)
        => throw new NotImplementedException();
}
