using EbenezerBackend.Features.Prayers.Domain.Entities;

namespace EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.Insert;

public record InsertPrayerRequestDto(
    string AuthorUsername,
    PrayerEntity PrayerEntity,
    string? CategoryId = null
    );
