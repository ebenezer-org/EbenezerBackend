namespace EbenezerBackend.Features.Profile.Presentation.Dtos.Register;

public record RegisterProfileRequestDto(
    string FullName,
    string Bio,
    string Phone
    );