using System.Collections.Generic;
using EbenezerBackend.Features.Prayers.Domain.Entities;

namespace EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.Insert;

public record InsertPrayerRequestDto(
    string AuthorUsername,
    PrayerEntity PrayerEntity,
    List<string>? CategoryIds = null
    );
