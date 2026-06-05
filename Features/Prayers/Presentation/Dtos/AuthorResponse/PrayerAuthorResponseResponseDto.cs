using EbenezerBackend.Features.Prayers.Presentation.Dtos.Shared.AuthorResponse;

namespace EbenezerBackend.Features.Prayers.Presentation.Dtos.AuthorResponse;

public record PrayerAuthorResponseResponseDto(
    string PrayerId,
    PrayerAuthorResponseDto Response
);

