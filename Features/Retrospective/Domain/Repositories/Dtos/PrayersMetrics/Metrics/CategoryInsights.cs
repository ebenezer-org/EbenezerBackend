namespace EbenezerBackend.Features.Retrospective.Domain.Repositories.Dtos.PrayersMetrics.Metrics;

public record CategoryInsights(
    CategoryInsightItem MostRequestedCategory,
    CategoryInsightItem LeastRequestedCategory,
    CategoryInsightItem MostAnsweredCategory
    );