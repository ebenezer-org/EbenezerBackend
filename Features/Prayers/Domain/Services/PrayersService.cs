using EbenezerBackend.Features.Prayers.Domain.Entities;
using EbenezerBackend.Features.Prayers.Domain.Repositories;
using EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.Insert;
using EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.Shared;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.AuthorResponse;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.Create;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.Get;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.List;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.Search;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.Shared;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.Shared.AuthorResponse;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.Support;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.Timeline;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.Update;
using EbenezerBackend.Shared.Dtos;
using EbenezerBackend.Shared.Exceptions;
using EbenezerBackend.Shared.Services.UserContext;

namespace EbenezerBackend.Features.Prayers.Domain.Services;

public class PrayersService(IPrayersRepository prayersRepository, IUserContext userContext) : IPrayersService
{
    public async Task<CreatePrayerResponseDto> CreatePostAsync(CreatePrayerRequestDto request, CancellationToken ct)
    {
        var username = userContext.UserName;
        var prayerEntity = new PrayerEntity(request.Content, request.IsPublic);
        var categoryIds = request.CategoryIds ?? [];

        var insertPrayerRequest = new InsertPrayerRequestDto(username, prayerEntity, categoryIds);
        var insertPrayerResult = await prayersRepository.InsertPrayerAsync(insertPrayerRequest, ct);

        var prayerResult = insertPrayerResult.PrayerModel.ToEntity();
        var userProfileResult = insertPrayerResult.AuthorProfileModel.ToEntity();
        var userSafeDto = new UserSafeDto(userProfileResult.UserName, userProfileResult.FullName);

        return new CreatePrayerResponseDto(
            prayerResult.Id ?? string.Empty,
            userSafeDto,
            prayerResult.Content,
            prayerResult.IsPublic,
            prayerResult.CreatedAt,
            prayerResult.UpdatedAt,
            ToCategoriesResponse(insertPrayerResult.Categories),
            ToAuthorResponse(prayerResult));
    }

    public async Task<GetPrayerResponseDto> GetPrayerByIdAsync(string prayerId, CancellationToken ct)
    {
        var viewerUserName = userContext.IsAuthenticated ? userContext.UserName : null;
        var result = await prayersRepository.FindPrayerByIdAsync(prayerId, viewerUserName, ct);

        if (result is null)
        {
            throw new NotFoundException("Oração não encontrada");
        }

        var prayer = result.PrayerModel.ToEntity();
        var author = result.AuthorProfileModel.ToEntity();
        var userSafeDto = new UserSafeDto(author.UserName, author.FullName);

        return new GetPrayerResponseDto(
            prayer.Id ?? string.Empty,
            userSafeDto,
            prayer.Content,
            prayer.IsPublic,
            prayer.CreatedAt,
            prayer.UpdatedAt,
            ToCategoriesResponse(result.Categories),
            ToAuthorResponse(prayer));
    }

    public async Task<UpdatePrayerResponseDto> UpdatePrayerAsync(string prayerId, UpdatePrayerRequestDto request, CancellationToken ct)
    {
        var updated = await prayersRepository.UpdatePrayerAsync(
            prayerId,
            userContext.UserName,
            request.Content,
            request.IsPublic,
            request.CategoryIds,
            ct);

        if (updated is null)
        {
            throw new NotFoundException("Oração não encontrada");
        }

        var prayer = updated.PrayerModel.ToEntity();
        var author = updated.AuthorProfileModel.ToEntity();
        var userSafeDto = new UserSafeDto(author.UserName, author.FullName);

        return new UpdatePrayerResponseDto(
            prayer.Id ?? string.Empty,
            userSafeDto,
            prayer.Content,
            prayer.IsPublic,
            prayer.CreatedAt,
            prayer.UpdatedAt,
            ToCategoriesResponse(updated.Categories),
            ToAuthorResponse(prayer));
    }

    public async Task DeletePrayerAsync(string prayerId, CancellationToken ct)
    {
        var deleted = await prayersRepository.DeletePrayerAsync(prayerId, userContext.UserName, ct);

        if (!deleted)
        {
            throw new NotFoundException("Oração não encontrada");
        }
    }

    public async Task<SupportReactionResponseDto> AddSupportReactionAsync(string prayerId, CancellationToken ct)
    {
        var reaction = await prayersRepository.ToggleSupportReactionAsync(prayerId, userContext.UserName, ct);

        if (reaction is null)
        {
            throw new NotFoundException("Oração não encontrada");
        }

        var author = reaction.AuthorProfileModel.ToEntity();
        var reactedBy = new UserSafeDto(author.UserName, author.FullName);

        return new SupportReactionResponseDto(
            reaction.PrayerModel.Id ?? string.Empty,
            reactedBy,
            reaction.ActivityAt);
    }

    public async Task<PaginatedResponseDto<IReadOnlyCollection<TimelinePrayerResponseDto>>> GetTimelineAsync(int page, int pageSize, CancellationToken ct)
    {
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize < 1 ? 20 : pageSize;
        var (timeline, totalCount) = await prayersRepository.GetTimelineAsync(userContext.UserName, safePage, safePageSize, ct);

        var items = timeline.Select(item =>
        {
            var prayer = item.PrayerModel.ToEntity();
            var author = item.AuthorProfileModel.ToEntity();
            var authorSafe = new UserSafeDto(author.UserName, author.FullName);

            var interactions = item.Interactions.Select(interaction =>
                new PrayerInteractionResponseDto(
                    interaction.Type,
                    new UserSafeDto(interaction.UserName, interaction.FullName),
                    interaction.CreatedAt)).ToList();

            return new TimelinePrayerResponseDto(
                prayer.Id ?? string.Empty,
                authorSafe,
                prayer.Content,
                prayer.IsPublic,
                prayer.CreatedAt,
                prayer.UpdatedAt,
                ToCategoriesResponse(item.Categories),
                interactions,
                item.ActivityAt,
                ToAuthorResponse(prayer));
        }).ToList();

        return new PaginatedResponseDto<IReadOnlyCollection<TimelinePrayerResponseDto>>(
            Items: items, Page: safePage, PageSize: safePageSize, TotalCount: totalCount);
    }

    public async Task<IReadOnlyCollection<ListPrayerResponseDto>> SearchPrayersAsync(SearchPrayersRequestDto request, CancellationToken ct)
    {
        var viewerUserName = userContext.IsAuthenticated ? userContext.UserName : null;
        var safePage = request.Page < 1 ? 1 : request.Page;
        var safePageSize = request.PageSize < 1 ? 20 : request.PageSize;
        var prayers = await prayersRepository.SearchPrayersAsync(
            viewerUserName,
            request.AuthorUserName,
            request.CategoryId,
            request.Text,
            safePage,
            safePageSize,
            ct);

        return prayers.Select(prayer =>
        {
            var prayerResult = prayer.PrayerModel.ToEntity();
            var userProfileResult = prayer.AuthorProfileModel.ToEntity();
            var userSafeDto = new UserSafeDto(userProfileResult.UserName, userProfileResult.FullName);

            return new ListPrayerResponseDto(
                prayerResult.Id ?? string.Empty,
                userSafeDto,
                prayerResult.Content,
                prayerResult.IsPublic,
                prayerResult.CreatedAt,
                prayerResult.UpdatedAt,
                ToCategoriesResponse(prayer.Categories),
                ToAuthorResponse(prayerResult));
        }).ToList();
    }

    private static IReadOnlyCollection<PrayerCategoryResponseDto> ToCategoriesResponse(
        IReadOnlyCollection<PrayerCategoryPartialDto> categories)
        => categories.Select(category => new PrayerCategoryResponseDto(category.Id, category.Name, category.ColorHex)).ToList();

    private static PrayerAuthorResponseDto? ToAuthorResponse(PrayerEntity prayer)
    {
        return string.IsNullOrWhiteSpace(prayer.AuthorResponseStatus) || prayer.AuthorResponseCreatedAt is null
            ? null
            : new PrayerAuthorResponseDto(prayer.AuthorResponseStatus, prayer.AuthorResponseMessage, prayer.AuthorResponseCreatedAt.Value);
    }
}
