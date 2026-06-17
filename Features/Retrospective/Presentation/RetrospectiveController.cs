using EbenezerBackend.Features.Retrospective.Domain.Services;
using EbenezerBackend.Features.Retrospective.Presentation.Dtos.GetMyRetrospective;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EbenezerBackend.Features.Retrospective.Presentation;

[ApiController]
[Route("[controller]")]
public class RetrospectiveController(IRetrospectiveService retrospectiveService) : ControllerBase
{
    [Authorize]
    [HttpGet("my-retrospective")]
    public async Task<ActionResult<GetMyRetrospectiveResponseDto>> GetMyRetrospective([FromQuery] GetMyRetrospectiveRequestDto request, CancellationToken ct)
    {
        var result = await retrospectiveService.GetMyRetrospectiveAsync(request, ct);
        
        return Ok(result);
    }
}