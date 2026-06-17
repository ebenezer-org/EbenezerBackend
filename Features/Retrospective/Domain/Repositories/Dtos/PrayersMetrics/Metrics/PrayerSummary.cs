namespace EbenezerBackend.Features.Retrospective.Domain.Repositories.Dtos.PrayersMetrics.Metrics;

public record PrayerSummary(
    int TotalPrayers,
    int AnsweredCount,
    int GrantedCount,
    int SovereignNoCount,
    int WaitCount
);