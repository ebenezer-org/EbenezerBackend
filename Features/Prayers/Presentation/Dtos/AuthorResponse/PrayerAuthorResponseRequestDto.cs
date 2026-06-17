using EbenezerBackend.Features.Prayers.Domain.Enums;

namespace EbenezerBackend.Features.Prayers.Presentation.Dtos.AuthorResponse;

public record PrayerAuthorResponseRequestDto(
    PrayerAnswerStatusEnum StatusEnum,
    string? Message = null
);

