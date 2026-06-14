using System.Threading.Tasks;
using EbenezerBackend.Features.Profile.Domain.Services;
using EbenezerBackend.Features.Profile.Presentation.Dtos.Register;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EbenezerBackend.Features.Profile.Presentation;

[ApiController]
[Route("[controller]")]
public class ProfileController(IProfileService service) : ControllerBase
{
    [HttpPost("new")]
    public async Task<IActionResult> RegisterProfile(RegisterProfileRequestDto request, CancellationToken ct)
    {
        var response = await service.RegisterProfileAsync(request, ct);
        
        return Ok(response);
    }
    
    [Authorize]
    [HttpGet("{username}")]
    public async Task<IActionResult> GetUser([FromRoute] string username, CancellationToken ct)
    {
        var response = await service.GetProfileAsync(username, ct);
        
        return Ok(response);
    }
}