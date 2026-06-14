using System;
using EbenezerBackend.Shared.Web.Dtos;

namespace EbenezerBackend.Features.Prayers.Presentation.Dtos.Support;

public record SupportReactionResponseDto(
    string PrayerId,
    UserEssentialDto ReactedBy,
    DateTime ReactedAt
);

