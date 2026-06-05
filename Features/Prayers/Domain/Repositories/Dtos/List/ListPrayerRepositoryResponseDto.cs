using EbenezerBackend.Features.Prayers.Data.Models;
using EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.Shared;
using EbenezerBackend.Features.Profile.Data.Models;

namespace EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.List;

public record ListPrayerRepositoryResponseDto(
    ProfileModel AuthorProfileModel,
    PrayerModel PrayerModel,
    PrayerCategoryPartialDto? Category
);
