
using EbenezerBackend.Features.Retrospective.Domain.Repositories.Dtos.PrayersMetrics.Metrics;

namespace EbenezerBackend.Features.Retrospective.Domain.Repositories.Dtos.PrayersMetrics;

public record PrayersMetricsResponseDto(
    PrayerSummary Summary,
    CategoryInsights Categories,
    FellowshipSummary Fellowship
);