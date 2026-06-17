namespace EbenezerBackend.Features.Retrospective.Domain.Repositories.Dtos.PrayersMetrics.Metrics;

public record FellowshipSummary(
    int NewFriendsCount,
    int IntercessionPartnersCount,
    int TotalEncouragementsReceived
);