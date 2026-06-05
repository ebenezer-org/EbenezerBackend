using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EbenezerBackend.Features.Prayers.Domain.Services;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.Create;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.List;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EbenezerBackend.Features.Prayers.Presentation;

[ApiController]
[Route("[controller]")]
public class PrayersController(IPrayersService prayersService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ListPrayerResponseDto>>> ListPrayers(CancellationToken ct)
    {
        var result = await prayersService.ListPrayersAsync(ct);

        return Ok(result);
    }

    [Authorize]
    [HttpPost("new")]
    public async Task<IActionResult> CreatePost([FromBody] CreatePrayerRequestDto request, CancellationToken ct)
    {
        var result = await prayersService.CreatePostAsync(request, ct);

        return Ok(result);
    }
}
