using EbenezerBackend.Features.Retrospective.Presentation.Dtos.GetMyRetrospective;

namespace EbenezerBackend.Features.Retrospective.Domain.Services;

public interface IRetrospectiveService
{
    Task<GetMyRetrospectiveResponseDto> GetMyRetrospectiveAsync(GetMyRetrospectiveRequestDto request, CancellationToken ct);

}