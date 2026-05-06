namespace EbenezerBackend.Features.Auth.Presentation.Dtos.Register;

public record RegisterRequestDto(
    string Username,
    string FullName,
    string Email,
    string Password
    );