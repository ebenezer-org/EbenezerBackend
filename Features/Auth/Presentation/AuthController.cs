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
        var token = await authService.RegisterAsync(dto.Username, dto.FullName, dto.Email, dto.Password);
        
        var response = new RegisterResponseDto(token);
        
        return CreatedAtAction(string.Empty, response);
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginRequestDto dto)
    {
        var token = await authService.LoginAsync(dto.Username, dto.Password);
        
        var response = new LoginResponseDto(token);
        
        return Ok(response);
    }
}