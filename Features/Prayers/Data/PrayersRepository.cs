using ArangoDBNetStandard;
using EbenezerBackend.Features.Prayers.Data.Models;
using EbenezerBackend.Features.Prayers.Domain.Repositories;
using EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.Insert;
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
            
            INSERT @prayer IN Prayers
            LET newPrayer = NEW
            
            INSERT { 
                _from: user._id,
                _to: newPrayer._id,
                CreatedAt: DATE_ISO8601(DATE_NOW())
            } IN PostedBy
            
            RETURN {
                AuthorProfileModel: user,
                PrayerModel: newPrayer
            }
        ";

        var prayerModel = PrayerModel.FromEntity(request.PrayerEntity);

        var bindVars = new Dictionary<string, object>
        {
            { "prayer", prayerModel },
            { "userName", request.AuthorUsername }
        };

        var response = await db.Cursor.PostCursorAsync<InsertPrayerResponseDto>(query, bindVars, token: ct );
        
        var result = response.Result.FirstOrDefault();
        
        return result ?? throw new InternalException("Database failed to return created Prayer");
    }
}
