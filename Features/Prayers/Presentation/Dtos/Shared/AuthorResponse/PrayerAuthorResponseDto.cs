using System;

namespace EbenezerBackend.Features.Prayers.Presentation.Dtos.Shared.AuthorResponse;

public record PrayerAuthorResponseDto(
    string Status,
    string? Message,
    DateTime CreatedAt
);

