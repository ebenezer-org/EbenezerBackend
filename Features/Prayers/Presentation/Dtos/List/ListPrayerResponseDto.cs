using System;
using System.Collections.Generic;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.Shared;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.Shared.AuthorResponse;
using EbenezerBackend.Shared.Dtos;

namespace EbenezerBackend.Features.Prayers.Presentation.Dtos.List;

public record ListPrayerResponseDto(
    string Id,
    UserSafeDto Author,
    string Content,
    bool IsPublic,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyCollection<PrayerCategoryResponseDto> Categories,
    PrayerAuthorResponseDto? AuthorResponse
);
