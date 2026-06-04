using EbenezerBackend.Features.Prayers.Domain.Entities;
using EbenezerBackend.Features.Prayers.Domain.Repositories;
using EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.Insert;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.Create;
using EbenezerBackend.Features.Profile.Domain.Repositories;
using EbenezerBackend.Shared.Dtos;
using EbenezerBackend.Shared.Services.UserContext;

namespace EbenezerBackend.Features.Prayers.Domain.Services;

public class PrayersService(IPrayersRepository prayersRepository, IProfileRepository profileRepository, IUserContext userContext) : IPrayersService
{
    public async Task<CreatePrayerResponseDto> CreatePostAsync(CreatePrayerRequestDto request, CancellationToken ct)
    {
        var username = userContext.UserName;
        var prayerEntity = new PrayerEntity(request.Content);

        var insertPrayerRequest = new InsertPrayerRequestDto(username, prayerEntity);
            
        var insertPrayerResult = await prayersRepository.InsertPrayerAsync(insertPrayerRequest, ct);

        var prayerResult = insertPrayerResult.PrayerModel.ToEntity();
        var userProfileResult = insertPrayerResult.AuthorProfileModel.ToEntity();
        
        var userSafeDto = new UserSafeDto(userProfileResult.UserName, userProfileResult.FullName);
        
        return new CreatePrayerResponseDto(userSafeDto, prayerResult.Content, prayerResult.CreatedAt);
    }
}