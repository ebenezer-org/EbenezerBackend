using System;

namespace EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.Timeline;

public record PrayerInteractionPartialDto(
    string Type,
    string UserName,
    string FullName,
    DateTime CreatedAt
);

