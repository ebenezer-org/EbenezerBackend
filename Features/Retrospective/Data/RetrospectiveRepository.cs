using EbenezerBackend.Features.Retrospective.Data.Models;
using EbenezerBackend.Features.Retrospective.Domain.Entities;
using EbenezerBackend.Features.Retrospective.Domain.Enums;
using EbenezerBackend.Features.Retrospective.Domain.Repositories;
using EbenezerBackend.Features.Retrospective.Domain.Repositories.Dtos.PrayersMetrics;
using EbenezerBackend.Shared.Data;

namespace EbenezerBackend.Features.Retrospective.Data;

public class RetrospectiveRepository : BaseRepository<EncouragementMessageModel>, IRetrospectiveRepository
{
    public Task<PrayersMetricsResponseDto?> GetMetrics(string userId, DateTime fromDate, DateTime toDate, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<EncouragementMessageEntity> FindEncouragementMessagesByCategoriesAsync(
        EncouragementMessageCategoryEnum encouragementMessageCategory, CancellationToken ct)
        => throw new NotImplementedException();
}
