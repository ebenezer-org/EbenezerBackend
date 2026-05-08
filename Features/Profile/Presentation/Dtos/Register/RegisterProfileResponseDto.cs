namespace EbenezerBackend.Features.Profile.Presentation.Dtos.Register;

public record RegisterProfileResponseDto(
    string UserName,
    string FullName,
    string Bio,
    string Phone
    );