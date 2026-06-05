using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EbenezerBackend.Features.Prayers.Domain.Entities;
using EbenezerBackend.Features.Prayers.Domain.Repositories;
using EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.Shared;
using EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.Insert;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.Create;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.List;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.Shared;
using EbenezerBackend.Shared.Dtos;
using EbenezerBackend.Shared.Services.UserContext;

namespace EbenezerBackend.Features.Prayers.Domain.Services;

public class PrayersService(IPrayersRepository prayersRepository, IUserContext userContext) : IPrayersService
{
    public async Task<CreatePrayerResponseDto> CreatePostAsync(CreatePrayerRequestDto request, CancellationToken ct)
    {
        var username = userContext.UserName;
        var prayerEntity = new PrayerEntity(request.Content);

        var insertPrayerRequest = new InsertPrayerRequestDto(username, prayerEntity, request.CategoryId);
            
        var insertPrayerResult = await prayersRepository.InsertPrayerAsync(insertPrayerRequest, ct);

        var prayerResult = insertPrayerResult.PrayerModel.ToEntity();
        var userProfileResult = insertPrayerResult.AuthorProfileModel.ToEntity();
        
        var userSafeDto = new UserSafeDto(userProfileResult.UserName, userProfileResult.FullName);
        
        return new CreatePrayerResponseDto(
            userSafeDto,
            prayerResult.Content,
            prayerResult.CreatedAt,
            ToCategoryResponse(insertPrayerResult.Category));
    }

    public async Task<IReadOnlyCollection<ListPrayerResponseDto>> ListPrayersAsync(CancellationToken ct)
    {
        var prayers = await prayersRepository.ListPrayersAsync(ct);

        return prayers.Select(prayer =>
        {
            var prayerResult = prayer.PrayerModel.ToEntity();
            var userProfileResult = prayer.AuthorProfileModel.ToEntity();
            var userSafeDto = new UserSafeDto(userProfileResult.UserName, userProfileResult.FullName);

            return new ListPrayerResponseDto(
                userSafeDto,
                prayerResult.Content,
                prayerResult.CreatedAt,
                ToCategoryResponse(prayer.Category));
        }).ToList();
    }

    private static PrayerCategoryResponseDto? ToCategoryResponse(PrayerCategoryPartialDto? category)
    {
        return category is null
            ? null
            : new PrayerCategoryResponseDto(category.Name, category.ColorHex);
    }
}
