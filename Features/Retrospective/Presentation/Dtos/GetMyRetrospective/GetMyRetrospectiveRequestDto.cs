namespace EbenezerBackend.Features.Retrospective.Presentation.Dtos.GetMyRetrospective;

public record GetMyRetrospectiveRequestDto
{
    public DateTime FromDate { get; init; } = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public DateTime ToDate { get; init; } = DateTime.UtcNow;
}