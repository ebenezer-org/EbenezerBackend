namespace EbenezerBackend.Features.Profile.Presentation.Dtos.Get;

public record GetProfileResponseDto(
    string FullName,
    string Bio,
    string Phone
    );