using EbenezerBackend.Features.Categories.Data.Models;
using EbenezerBackend.Features.Comments.Data.Models;
using EbenezerBackend.Features.Comments.Domain.Enums;
using EbenezerBackend.Features.Prayers.Data.Models;
using EbenezerBackend.Features.Prayers.Domain.Enums;
using EbenezerBackend.Features.Profile.Data.Models;
using EbenezerBackend.Features.Retrospective.Data.Models;
using EbenezerBackend.Features.Retrospective.Domain.Entities;
using EbenezerBackend.Features.Retrospective.Domain.Enums;
using EbenezerBackend.Features.Retrospective.Domain.Repositories;
using EbenezerBackend.Features.Retrospective.Domain.Repositories.Dtos.PrayersMetrics;
using EbenezerBackend.Features.Retrospective.Domain.Repositories.Dtos.PrayersMetrics.Metrics;
using EbenezerBackend.Infrastructure.Data;
using EbenezerBackend.Shared.Data;
using MongoDB.Bson;
using MongoDB.Driver;
using Neo4j.Driver;

namespace EbenezerBackend.Features.Retrospective.Data;

public class RetrospectiveRepository(IMongoDatabase database, INeo4JExecutor neo4J)
    : BaseRepository<EncouragementMessageModel>, IRetrospectiveRepository
{
    private readonly IMongoCollection<PrayerModel> _prayers =
        database.GetCollection<PrayerModel>(DbCollections.Prayers);

    private readonly IMongoCollection<CommentsModel> _comments =
        database.GetCollection<CommentsModel>(DbCollections.Comments);

    private readonly IMongoCollection<CategoryModel> _categories =
        database.GetCollection<CategoryModel>(DbCollections.Categories);

    private readonly IMongoCollection<ProfileModel> _profiles =
        database.GetCollection<ProfileModel>(DbCollections.Users);

    private readonly IMongoCollection<EncouragementMessageModel> _encouragementMessages =
        database.GetCollection<EncouragementMessageModel>(CollectionName);


    public async Task<PrayersMetricsResponseDto?> GetMetrics(
        string userId, DateTime fromDate, DateTime toDate, CancellationToken ct)
    {
        var profile = await _profiles
            .Find(Builders<ProfileModel>.Filter.Eq(u => u.Id, userId))
            .FirstOrDefaultAsync(ct);

        if (profile is null)
            return null;

        var authorUsername = profile.UserName;

        var prayers = await _prayers
            .Find(Builders<PrayerModel>.Filter.And(
                Builders<PrayerModel>.Filter.Eq(p => p.AuthorUsername, authorUsername),
                Builders<PrayerModel>.Filter.Gte(p => p.CreatedAt, fromDate),
                Builders<PrayerModel>.Filter.Lte(p => p.CreatedAt, toDate)))
            .ToListAsync(ct);

        if (prayers.Count == 0)
        {
            return new PrayersMetricsResponseDto(
                Summary: new PrayerSummary(
                    TotalPrayers: 0,
                    AnsweredCount: 0,
                    GrantedCount: 0,
                    SovereignNoCount: 0,
                    WaitCount: 0),
                Categories: new CategoryInsights(
                    MostRequestedCategory: new CategoryInsightItem("Nenhuma", 0),
                    LeastRequestedCategory: new CategoryInsightItem("Nenhuma", 0),
                    MostAnsweredCategory: new CategoryInsightItem("Nenhuma", 0)),
                Fellowship: new FellowshipSummary(
                    NewFriendsCount: 0,
                    IntercessionPartnersCount: 0,
                    TotalEncouragementsReceived: 0)
            );
        }

        var prayerIds = prayers.Select(p => p.Id!).ToList();

        var summary         = BuildPrayerSummary(prayers);
        var categoryInsights = await BuildCategoryInsightsAsync(prayers, ct);
        var fellowship       = await BuildFellowshipAsync(userId, authorUsername, prayerIds, fromDate, toDate, ct);

        return new PrayersMetricsResponseDto(summary, categoryInsights, fellowship);
    }


    public async Task<EncouragementMessageEntity> FindEncouragementMessagesByCategoriesAsync(
        EncouragementMessageCategoryEnum encouragementMessageCategory, CancellationToken ct)
    {
        var model = await _encouragementMessages
            .Find(Builders<EncouragementMessageModel>.Filter.Eq(m => m.Category, encouragementMessageCategory))
            .FirstOrDefaultAsync(ct);

        if (model is null && encouragementMessageCategory != EncouragementMessageCategoryEnum.Default)
        {
            model = await _encouragementMessages
                .Find(Builders<EncouragementMessageModel>.Filter.Eq(
                    m => m.Category, EncouragementMessageCategoryEnum.Default))
                .FirstOrDefaultAsync(ct);
        }

        if (model is null)
            throw new KeyNotFoundException(
                $"Mensagem de encorajamento não encontrada para a categoria '{encouragementMessageCategory}'.");

        return model.ToEntity();
    }


    private static PrayerSummary BuildPrayerSummary(IReadOnlyList<PrayerModel> prayers)
    {
        var total       = prayers.Count;
        var granted     = prayers.Count(p => p.DivineAnswerStatus == PrayerAnswerStatusEnum.Granted);
        var sovereignNo = prayers.Count(p => p.DivineAnswerStatus == PrayerAnswerStatusEnum.SovereignNo);
        var wait        = prayers.Count(p => p.DivineAnswerStatus == PrayerAnswerStatusEnum.Wait);
        var answered    = prayers.Count(p => p.DivineAnswerStatus is not null);

        return new PrayerSummary(total, answered, granted, sovereignNo, wait);
    }


    private async Task<CategoryInsights> BuildCategoryInsightsAsync(
        IReadOnlyList<PrayerModel> prayers, CancellationToken ct)
    {
        var defaultItem = new CategoryInsightItem("Nenhuma", 0);

        var generalCounts = prayers
            .SelectMany(p => p.CategoryIds)
            .GroupBy(id => id)
            .Select(g => (CategoryId: g.Key, Count: g.Count()))
            .ToList();

        if (generalCounts.Count == 0)
            return new CategoryInsights(defaultItem, defaultItem, defaultItem);

        var answeredCounts = prayers
            .Where(p => p.DivineAnswerStatus is not null)
            .SelectMany(p => p.CategoryIds)
            .GroupBy(id => id)
            .Select(g => (CategoryId: g.Key, Count: g.Count()))
            .ToList();

        var allIds  = generalCounts.Select(x => x.CategoryId)
            .Concat(answeredCounts.Select(x => x.CategoryId))
            .Distinct()
            .ToList();

        var nameMap = await LoadCategoryNamesAsync(allIds, ct);

        CategoryInsightItem ToItem((string CategoryId, int Count) x) =>
            new(nameMap.TryGetValue(x.CategoryId, out var name) ? name : x.CategoryId, x.Count);

        var mostRequested  = ToItem(generalCounts.MaxBy(x => x.Count)!);
        var leastRequested = ToItem(generalCounts.MinBy(x => x.Count)!);
        var mostAnswered   = answeredCounts.Count > 0
            ? ToItem(answeredCounts.MaxBy(x => x.Count)!)
            : defaultItem;

        return new CategoryInsights(mostRequested, leastRequested, mostAnswered);
    }


    private async Task<FellowshipSummary> BuildFellowshipAsync(
        string userId,
        string authorUsername,
        IReadOnlyList<string> prayerIds,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken ct)
    {
        var newFriendsCount = await CountNewFriendsAsync(userId, fromDate, toDate, ct);

        var commentAuthorIds = await _comments
            .Find(Builders<CommentsModel>.Filter.And(
                Builders<CommentsModel>.Filter.In(c => c.ParentId, prayerIds),
                Builders<CommentsModel>.Filter.Eq(c => c.ParentType, CommentParentTypeEnum.Prayer),
                Builders<CommentsModel>.Filter.Ne(c => c.AuthorId, userId),
                Builders<CommentsModel>.Filter.Eq(c => c.DeletedAt, null)))
            .Project(c => c.AuthorId)
            .ToListAsync(ct);

        var supporterUserNames = (await _prayers
            .Find(Builders<PrayerModel>.Filter.In(p => p.Id, prayerIds))
            .Project(p => p.Supporters)
            .ToListAsync(ct))
            .SelectMany(supporters => supporters)
            .Where(s => s.UserName != authorUsername)
            .Select(s => s.UserName)
            .ToList();

        var totalEncouragementsReceived = commentAuthorIds.Count + supporterUserNames.Count;

        var commenterUserNames = await ResolveUserNamesByIdsAsync(commentAuthorIds.Distinct().ToList(), ct);

        var uniqueIntercessors = commenterUserNames
            .Concat(supporterUserNames)
            .Distinct(StringComparer.Ordinal)
            .Count();

        return new FellowshipSummary(newFriendsCount, uniqueIntercessors, totalEncouragementsReceived);
    }


    private async Task<int> CountNewFriendsAsync(
        string userId, DateTime fromDate, DateTime toDate, CancellationToken ct)
    {
        var query = $$"""
            MATCH (u:User {id: $userId})-[f:{{DbEdges.Friendships}}]-(:User)
            WHERE f.acceptedAt IS NOT NULL
              AND f.acceptedAt >= $fromDate
              AND f.acceptedAt <= $toDate
            RETURN COUNT(f) AS total
            """;

        return await neo4J.ExecuteReadSingleAsync(
            query,
            new
            {
                userId,
                fromDate = Neo4JValueConverter.ToIso8601(fromDate),
                toDate   = Neo4JValueConverter.ToIso8601(toDate)
            },
            record => record["total"].As<int>(),
            ct);
    }

    private async Task<Dictionary<string, string>> LoadCategoryNamesAsync(
        IEnumerable<string> categoryIds, CancellationToken ct)
    {
        var validIds = categoryIds
            .Where(id => ObjectId.TryParse(id, out _))
            .Distinct()
            .ToList();

        if (validIds.Count == 0)
            return [];

        var categories = await _categories
            .Find(Builders<CategoryModel>.Filter.In(c => c.Id, validIds))
            .Project(c => new { c.Id, c.Name })
            .ToListAsync(ct);

        return categories
            .Where(c => c.Id is not null)
            .ToDictionary(c => c.Id!, c => c.Name);
    }

    private async Task<List<string>> ResolveUserNamesByIdsAsync(
        IReadOnlyList<string> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0)
            return [];

        var profiles = await _profiles
            .Find(Builders<ProfileModel>.Filter.In(u => u.Id, userIds))
            .Project(u => u.UserName)
            .ToListAsync(ct);

        return profiles;
    }
}
