namespace EbenezerBackend.Features.Auth.Presentation.Dtos.Login;

public record LoginRequestDto(
    string Username,
    string Password);