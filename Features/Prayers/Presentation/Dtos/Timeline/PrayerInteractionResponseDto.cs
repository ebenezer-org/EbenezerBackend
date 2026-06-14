using System;
using EbenezerBackend.Shared.Web.Dtos;

namespace EbenezerBackend.Features.Prayers.Presentation.Dtos.Timeline;

public record PrayerInteractionResponseDto(
    string Type,
    UserEssentialDto User,
    DateTime CreatedAt
);

