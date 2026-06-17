using EbenezerBackend.Features.Retrospective.Domain.Entities;
using EbenezerBackend.Features.Retrospective.Domain.Enums;
using EbenezerBackend.Features.Retrospective.Domain.Repositories.Dtos.PrayersMetrics;

namespace EbenezerBackend.Features.Retrospective.Domain.Repositories;

public interface IRetrospectiveRepository
{
    Task<PrayersMetricsResponseDto?> GetMetrics(string userId, DateTime fromDate, DateTime toDate, CancellationToken ct);
    Task<EncouragementMessageEntity> FindEncouragementMessagesByCategoriesAsync(
        EncouragementMessageCategoryEnum encouragementMessageCategory, CancellationToken ct);
}