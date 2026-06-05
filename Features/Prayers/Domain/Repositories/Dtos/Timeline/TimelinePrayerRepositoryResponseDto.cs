using System;
using System.Collections.Generic;
using EbenezerBackend.Features.Prayers.Data.Models;
using EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.Shared;
using EbenezerBackend.Features.Profile.Data.Models;

namespace EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.Timeline;

public record TimelinePrayerRepositoryResponseDto(
    ProfileModel AuthorProfileModel,
    PrayerModel PrayerModel,
    IReadOnlyCollection<PrayerCategoryPartialDto> Categories,
    IReadOnlyCollection<PrayerInteractionPartialDto> Interactions,
    DateTime ActivityAt
);

