using EbenezerBackend.Shared.Dtos;

namespace EbenezerBackend.Features.Prayers.Presentation.Dtos.Create;

public record CreatePrayerResponseDto(
    UserSafeDto Author,
    string Content,
    DateTime CreatedAt
    );