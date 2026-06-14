using System.Threading.Tasks;
using EbenezerBackend.Features.Auth.Domain.Services.Interfaces;
using EbenezerBackend.Features.Auth.Presentation.Dtos.Login;
using EbenezerBackend.Features.Auth.Presentation.Dtos.Register;
using Microsoft.AspNetCore.Mvc;

namespace EbenezerBackend.Features.Auth.Presentation;

[ApiController]
[Route("[controller]")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<RegisterResponseDto>> Register(RegisterRequestDto dto)
    {
        var token = await authService.RegisterAsync(dto.UserName, dto.Email, dto.Password);
        
        var response = new RegisterResponseDto(token);
        
        return Created($"/user/{dto.UserName}", response);
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginRequestDto dto)
    {
        var token = await authService.LoginAsync(dto.UserName, dto.Password);
        
        var response = new LoginResponseDto(token);
        
        return Ok(response);
    }
}