namespace EbenezerBackend.Features.Auth.Presentation.Dtos.Register;

public record RegisterRequestDto(
    string Username,
    string Email,
    string Password
    );