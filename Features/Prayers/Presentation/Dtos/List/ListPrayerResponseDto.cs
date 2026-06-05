using EbenezerBackend.Features.Prayers.Presentation.Dtos.Shared;
using EbenezerBackend.Shared.Dtos;

namespace EbenezerBackend.Features.Prayers.Presentation.Dtos.List;

public record ListPrayerResponseDto(
    UserSafeDto Author,
    string Content,
    DateTime CreatedAt,
    PrayerCategoryResponseDto? Category
);
