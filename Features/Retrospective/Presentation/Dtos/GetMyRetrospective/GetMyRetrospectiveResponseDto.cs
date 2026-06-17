using EbenezerBackend.Features.Retrospective.Domain.Repositories.Dtos.PrayersMetrics;

namespace EbenezerBackend.Features.Retrospective.Presentation.Dtos.GetMyRetrospective;

public record GetMyRetrospectiveResponseDto(
    string Title,
    string Message,
    string ScriptureVerse,
    string ScriptureReference,
    PrayersMetricsResponseDto? Metrics
    );