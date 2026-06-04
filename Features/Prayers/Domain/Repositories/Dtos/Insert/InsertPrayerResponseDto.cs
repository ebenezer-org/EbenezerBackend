using EbenezerBackend.Features.Prayers.Data.Models;
using EbenezerBackend.Features.Profile.Data.Models;

namespace EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.Insert;

public record InsertPrayerResponseDto(
    ProfileModel AuthorProfileModel,
    PrayerModel PrayerModel
    );