using System.Collections.Generic;
using EbenezerBackend.Features.Prayers.Data.Models;
using EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.Shared;
using EbenezerBackend.Features.Profile.Data.Models;

namespace EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.Insert;

public record InsertPrayerResponseDto(
    ProfileModel AuthorProfileModel,
    PrayerModel PrayerModel,
    List<PrayerCategoryPartialDto> Categories
    );
