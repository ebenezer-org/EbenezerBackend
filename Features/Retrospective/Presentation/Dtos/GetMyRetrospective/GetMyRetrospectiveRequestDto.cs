namespace EbenezerBackend.Features.Retrospective.Presentation.Dtos.GetMyRetrospective;

public record GetMyRetrospectiveRequestDto(
    DateTime FromDate,
    DateTime ToDate
    );