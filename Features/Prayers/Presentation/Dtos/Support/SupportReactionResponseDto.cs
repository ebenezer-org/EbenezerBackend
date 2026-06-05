using System;
using EbenezerBackend.Shared.Dtos;

namespace EbenezerBackend.Features.Prayers.Presentation.Dtos.Support;

public record SupportReactionResponseDto(
    string PrayerId,
    UserSafeDto ReactedBy,
    DateTime ReactedAt
);

