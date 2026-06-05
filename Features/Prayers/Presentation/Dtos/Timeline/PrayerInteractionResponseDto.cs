using System;
using EbenezerBackend.Shared.Dtos;

namespace EbenezerBackend.Features.Prayers.Presentation.Dtos.Timeline;

public record PrayerInteractionResponseDto(
    string Type,
    UserSafeDto User,
    DateTime CreatedAt
);

