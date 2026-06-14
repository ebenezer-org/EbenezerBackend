namespace EbenezerBackend.Features.Auth.Presentation.Dtos.Register;

public record RegisterRequestDto(
    string UserName,
    string Email,
    string Password
    );