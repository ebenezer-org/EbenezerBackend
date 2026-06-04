using EbenezerBackend.Features.Profile.Domain.Services.Interfaces;
using EbenezerBackend.Features.Profile.Presentation.Dtos.Register;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EbenezerBackend.Features.Profile.Presentation;

[ApiController]
[Route("[controller]")]
public class ProfileController(IProfileService service) : ControllerBase
{
    [Authorize]
    [HttpPost("new")]
    public async Task<IActionResult> RegisterProfile(RegisterProfileRequestDto request)
    {
        var response = await service.RegisterProfileAsync(request);
        
        return Ok(response);
    }
    
    [HttpGet("{username}")]
    public async Task<IActionResult> GetUser([FromRoute] string username)
    {
        var response = await service.GetProfileAsync(username);
        
        return Ok(response);
    }
}