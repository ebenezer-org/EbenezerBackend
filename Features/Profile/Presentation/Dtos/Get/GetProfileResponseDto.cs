namespace EbenezerBackend.Features.Profile.Presentation.Dtos.Get;

public record GetProfileResponseDto(
    string Id,
    string UserName,
    string FullName,
    string Bio,
    string Phone
    );