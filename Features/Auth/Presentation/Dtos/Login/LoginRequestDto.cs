namespace EbenezerBackend.Features.Auth.Presentation.Dtos.Login;

public record LoginRequestDto(
    string UserName,
    string Password);