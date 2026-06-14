using System;
using System.Collections.Generic;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.Shared;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.Shared.AuthorResponse;
using EbenezerBackend.Shared.Web.Dtos;

namespace EbenezerBackend.Features.Prayers.Presentation.Dtos.Update;

public record UpdatePrayerResponseDto(
    string Id,
    UserEssentialDto Author,
    string Content,
    bool IsPublic,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyCollection<PrayerCategoryResponseDto> Categories,
    PrayerAuthorResponseDto? AuthorResponse
);

