using ArangoDBNetStandard;
using EbenezerBackend.Features.Prayers.Data.Models;
using EbenezerBackend.Features.Prayers.Domain.Repositories;
using EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.Insert;
using EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.List;
using EbenezerBackend.Shared;
using EbenezerBackend.Shared.Exceptions;

namespace EbenezerBackend.Features.Prayers.Data;

public class PrayersRepository(IArangoDBClient db) : BaseRepository<PrayerModel>, IPrayersRepository
{
    public async Task<InsertPrayerResponseDto> InsertPrayerAsync(InsertPrayerRequestDto request, CancellationToken ct)
    {
        var query = @"
            LET user = FIRST(
                FOR u IN Users 
                FILTER u.UserName == @userName
                LIMIT 1
                RETURN u
            )

            FILTER user != null

            LET category = @categoryId == null || @categoryId == '' ? null : FIRST(
                FOR currentCategory IN Categories
                    FILTER currentCategory._key == @categoryId || currentCategory._id == @categoryId
                    LET ownerLink = FIRST(
                        FOR edge IN CreatedCategory
                            FILTER edge._from == user._id && edge._to == currentCategory._id
                            LIMIT 1
                            RETURN edge
                    )
                    FILTER ownerLink != null
                    RETURN currentCategory
            )

            FILTER @categoryId == null || @categoryId == '' || category != null
            
            INSERT @prayer INTO Prayers
            LET newPrayer = NEW
            
            INSERT { 
                _from: user._id,
                _to: newPrayer._id,
                CreatedAt: DATE_ISO8601(DATE_NOW())
            } INTO PostedBy

            LET categoryLink = (
                FOR categoryToLink IN category == null ? [] : [category]
                    INSERT {
                        _from: newPrayer._id,
                        _to: categoryToLink._id,
                        CreatedAt: DATE_ISO8601(DATE_NOW())
                    } INTO CategorizedAs
                    RETURN NEW
            )
            
            RETURN {
                AuthorProfileModel: user,
                PrayerModel: newPrayer,
                Category: category == null ? null : {
                    Name: category.Name,
                    ColorHex: category.ColorHex
                },
                CategoryLinkCount: LENGTH(categoryLink)
            }
        ";

        var prayerModel = PrayerModel.FromEntity(request.PrayerEntity);

        var bindVars = new Dictionary<string, object>
        {
            { "prayer", prayerModel },
            { "userName", request.AuthorUsername },
            { "categoryId", request.CategoryId ?? string.Empty }
        };

        var response = await db.Cursor.PostCursorAsync<InsertPrayerResponseDto>(query, bindVars, token: ct );
        
        var result = response.Result.FirstOrDefault();
        
        return result ?? throw new NotFoundException("Usuário ou categoria não encontrados");
    }

    public async Task<IReadOnlyCollection<ListPrayerRepositoryResponseDto>> ListPrayersAsync(CancellationToken ct)
    {
        var query = @"
            FOR prayer IN Prayers
                SORT prayer.CreatedAt DESC

                LET author = FIRST(
                    FOR user IN 1..1 INBOUND prayer._id PostedBy
                        LIMIT 1
                        RETURN user
                )

                FILTER author != null

                LET category = FIRST(
                    FOR currentCategory IN 1..1 OUTBOUND prayer._id CategorizedAs
                        LIMIT 1
                        RETURN {
                            Name: currentCategory.Name,
                            ColorHex: currentCategory.ColorHex
                        }
                )

                RETURN {
                    AuthorProfileModel: author,
                    PrayerModel: prayer,
                    Category: category
                }
        ";

        var response = await db.Cursor.PostCursorAsync<ListPrayerRepositoryResponseDto>(query, token: ct);

        return response.Result.ToList();
    }
}
