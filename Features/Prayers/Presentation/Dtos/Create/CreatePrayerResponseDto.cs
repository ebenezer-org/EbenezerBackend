using System;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.Shared;
using EbenezerBackend.Shared.Dtos;

namespace EbenezerBackend.Features.Prayers.Presentation.Dtos.Create;

public record CreatePrayerResponseDto(
    UserSafeDto Author,
    string Content,
    DateTime CreatedAt,
    PrayerCategoryResponseDto? Category
    );
