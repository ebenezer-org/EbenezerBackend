using EbenezerBackend.Features.Prayers.Domain.Services;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.Create;
using Microsoft.AspNetCore.Mvc;

namespace EbenezerBackend.Features.Prayers.Presentation;

[ApiController]
[Route("[controller]")]
public class PrayersController(IPrayersService prayersService) : ControllerBase
{
    [HttpPost("new")]
    public async Task<IActionResult> CreatePost([FromBody] CreatePrayerRequestDto request, CancellationToken ct)
    {
        var result = await prayersService.CreatePostAsync(request, ct);

        return Ok(result);
    }
}