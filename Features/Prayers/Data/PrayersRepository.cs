using System.Text.RegularExpressions;
using EbenezerBackend.Features.Categories.Data.Models;
using EbenezerBackend.Features.Prayers.Data.Models;
using EbenezerBackend.Features.Prayers.Domain.Repositories;
using EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.Insert;
using EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.List;
using EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.Shared;
using EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.Timeline;
using EbenezerBackend.Features.Profile.Data.Models;
using EbenezerBackend.Infrastructure.Data;
using EbenezerBackend.Shared.Data;
using EbenezerBackend.Shared.Web.Exceptions;
using MongoDB.Bson;
using MongoDB.Driver;
using Neo4j.Driver;

namespace EbenezerBackend.Features.Prayers.Data;

public class PrayersRepository : BaseRepository<PrayerModel>, IPrayersRepository
{
    private const string SupportInteractionType = "Support";

    private readonly IMongoCollection<PrayerModel> _prayers;
    private readonly IMongoCollection<ProfileModel> _users;
    private readonly IMongoCollection<CategoryModel> _categories;
    private readonly INeo4JExecutor _neo4j;

    public PrayersRepository(IMongoDatabase database, INeo4JExecutor neo4j)
    {
        _prayers = database.GetCollection<PrayerModel>(CollectionName);
        _users = database.GetCollection<ProfileModel>(DbCollections.Users);
        _categories = database.GetCollection<CategoryModel>(DbCollections.Categories);
        _neo4j = neo4j;
    }

    public async Task<InsertPrayerResponseDto> InsertPrayerAsync(InsertPrayerRequestDto request, CancellationToken ct)
    {
        var model = PrayerModel.FromEntity(request.PrayerEntity);
        model.Id = null;
        model.AuthorUsername = request.AuthorUsername;
        model.CategoryIds = (request.CategoryIds ?? []).Distinct().ToList();
        model.Supporters = [];

        await _prayers.InsertOneAsync(model, cancellationToken: ct);

        var author = await FindAuthorAsync(request.AuthorUsername, ct)
                     ?? throw new ProfileNotFoundException(request.AuthorUsername);
        var categories = await LoadCategoriesAsync(model.CategoryIds, ct);

        return new InsertPrayerResponseDto(author, model, categories);
    }

    public async Task<IReadOnlyCollection<ListPrayerRepositoryResponseDto>> ListPrayersAsync(
        string? viewerUserName, CancellationToken ct)
    {
        var filter = await BuildVisibilityFilterAsync(viewerUserName, ct);
        var sort = Builders<PrayerModel>.Sort.Descending(p => p.CreatedAt);

        var models = await _prayers.Find(filter).Sort(sort).ToListAsync(ct);

        return await ToListResponsesAsync(models, ct);
    }

    public async Task<ListPrayerRepositoryResponseDto?> FindPrayerByIdAsync(
        string prayerId, string? viewerUserName, CancellationToken ct)
    {
        if (!ObjectId.TryParse(prayerId, out _))
        {
            return null;
        }

        var filter = Builders<PrayerModel>.Filter.Eq(p => p.Id, prayerId);
        var model = await _prayers.Find(filter).FirstOrDefaultAsync(ct);

        if (model is null || !await CanViewAsync(model, viewerUserName, ct))
        {
            return null;
        }

        var author = await FindAuthorAsync(model.AuthorUsername, ct);
        if (author is null)
        {
            return null;
        }

        var categories = await LoadCategoriesAsync(model.CategoryIds, ct);
        return new ListPrayerRepositoryResponseDto(author, model, categories);
    }

    public async Task<ListPrayerRepositoryResponseDto?> UpdatePrayerAsync(
        string prayerId, string ownerUserName, string content, bool isPublic,
        IReadOnlyCollection<string> categoryIds, CancellationToken ct)
    {
        if (!ObjectId.TryParse(prayerId, out _))
        {
            return null;
        }

        var filter = Builders<PrayerModel>.Filter.And(
            Builders<PrayerModel>.Filter.Eq(p => p.Id, prayerId),
            Builders<PrayerModel>.Filter.Eq(p => p.AuthorUsername, ownerUserName));

        var update = Builders<PrayerModel>.Update
            .Set(p => p.Content, content)
            .Set(p => p.IsPublic, isPublic)
            .Set(p => p.CategoryIds, (categoryIds ?? []).Distinct().ToList())
            .Set(p => p.UpdatedAt, DateTime.UtcNow);

        var options = new FindOneAndUpdateOptions<PrayerModel> { ReturnDocument = ReturnDocument.After };
        var model = await _prayers.FindOneAndUpdateAsync(filter, update, options, ct);

        if (model is null)
        {
            return null;
        }

        var author = await FindAuthorAsync(model.AuthorUsername, ct);
        if (author is null)
        {
            return null;
        }

        var categories = await LoadCategoriesAsync(model.CategoryIds, ct);
        return new ListPrayerRepositoryResponseDto(author, model, categories);
    }

    public async Task<bool> DeletePrayerAsync(string prayerId, string ownerUserName, CancellationToken ct)
    {
        if (!ObjectId.TryParse(prayerId, out _))
        {
            return false;
        }

        var filter = Builders<PrayerModel>.Filter.And(
            Builders<PrayerModel>.Filter.Eq(p => p.Id, prayerId),
            Builders<PrayerModel>.Filter.Eq(p => p.AuthorUsername, ownerUserName));

        var result = await _prayers.DeleteOneAsync(filter, ct);
        return result.DeletedCount > 0;
    }

    public async Task<ListPrayerRepositoryResponseDto?> SetAuthorResponseAsync(
        string prayerId, string ownerUserName, string? message, CancellationToken ct)
    {
        if (!ObjectId.TryParse(prayerId, out _))
        {
            return null;
        }

        var filter = Builders<PrayerModel>.Filter.And(
            Builders<PrayerModel>.Filter.Eq(p => p.Id, prayerId),
            Builders<PrayerModel>.Filter.Eq(p => p.AuthorUsername, ownerUserName));

        var update = Builders<PrayerModel>.Update
            .Set(p => p.AuthorResponseMessage, message)
            .Set(p => p.AuthorResponseCreatedAt, DateTime.UtcNow);

        var options = new FindOneAndUpdateOptions<PrayerModel> { ReturnDocument = ReturnDocument.After };
        var model = await _prayers.FindOneAndUpdateAsync(filter, update, options, ct);

        if (model is null)
        {
            return null;
        }

        var author = await FindAuthorAsync(model.AuthorUsername, ct);
        if (author is null)
        {
            return null;
        }

        var categories = await LoadCategoriesAsync(model.CategoryIds, ct);
        return new ListPrayerRepositoryResponseDto(author, model, categories);
    }

    public async Task AddSupportReactionAsync(string prayerId, string reactorUserName, CancellationToken ct)
    {
        if (!ObjectId.TryParse(prayerId, out _))
        {
            return;
        }

        var filter = Builders<PrayerModel>.Filter.Eq(p => p.Id, prayerId);

        // Keep the reaction idempotent: drop any previous reaction from this user before re-adding it.
        var pull = Builders<PrayerModel>.Update.PullFilter(
            p => p.Supporters, s => s.UserName == reactorUserName);
        await _prayers.UpdateOneAsync(filter, pull, cancellationToken: ct);

        var existsFilter = Builders<PrayerModel>.Filter.Eq(p => p.Id, prayerId);
        var push = Builders<PrayerModel>.Update.Push(
            p => p.Supporters,
            new PrayerSupportReactionModel { UserName = reactorUserName, ReactedAt = DateTime.UtcNow });
        await _prayers.UpdateOneAsync(existsFilter, push, cancellationToken: ct);
    }

    public async Task RemoveSupportReactionAsync(string prayerId, string reactorUserName, CancellationToken ct)
    {
        if (!ObjectId.TryParse(prayerId, out _))
        {
            return;
        }

        var filter = Builders<PrayerModel>.Filter.Eq(p => p.Id, prayerId);
        var pull = Builders<PrayerModel>.Update.PullFilter(
            p => p.Supporters, s => s.UserName == reactorUserName);

        await _prayers.UpdateOneAsync(filter, pull, cancellationToken: ct);
    }

    public async Task<(IReadOnlyCollection<TimelinePrayerRepositoryResponseDto>, int TotalCount)> GetTimelineAsync(
        string viewerUserName, int page, int pageSize, CancellationToken ct)
    {
        // The timeline is the viewer's own prayers plus the prayers authored by their accepted friends.
        var friends = await GetAcceptedFriendUserNamesAsync(viewerUserName, ct);
        var feedAuthors = friends.Append(viewerUserName).Distinct().ToList();

        var filter = Builders<PrayerModel>.Filter.In(p => p.AuthorUsername, feedAuthors);
        var models = await _prayers.Find(filter).ToListAsync(ct);

        var ordered = models
            .OrderByDescending(ResolveActivityAt)
            .ToList();

        var totalCount = ordered.Count;

        var pageItems = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        if (pageItems.Count == 0)
        {
            return ([], totalCount);
        }

        var authorUserNames = pageItems.Select(p => p.AuthorUsername);
        var supporterUserNames = pageItems.SelectMany(p => p.Supporters.Select(s => s.UserName));
        var profiles = await LoadProfilesAsync(authorUserNames.Concat(supporterUserNames), ct);

        var categories = await LoadCategoryMapAsync(pageItems.SelectMany(p => p.CategoryIds), ct);

        var items = new List<TimelinePrayerRepositoryResponseDto>();
        foreach (var prayer in pageItems)
        {
            if (!profiles.TryGetValue(prayer.AuthorUsername, out var author))
            {
                continue;
            }

            var prayerCategories = ResolveCategories(prayer.CategoryIds, categories);

            var interactions = prayer.Supporters
                .OrderBy(s => s.ReactedAt)
                .Where(s => profiles.ContainsKey(s.UserName))
                .Select(s =>
                {
                    var reactor = profiles[s.UserName];
                    return new PrayerInteractionPartialDto(
                        SupportInteractionType, reactor.Id ?? string.Empty, reactor.UserName, reactor.FullName, s.ReactedAt);
                })
                .ToList();

            items.Add(new TimelinePrayerRepositoryResponseDto(
                author, prayer, prayerCategories, interactions, ResolveActivityAt(prayer)));
        }

        return (items, totalCount);
    }

    public async Task<IReadOnlyCollection<ListPrayerRepositoryResponseDto>> SearchPrayersAsync(
        string? viewerUserName, string? authorUserName, string? categoryId, string? text,
        int page, int pageSize, CancellationToken ct)
    {
        var filters = new List<FilterDefinition<PrayerModel>>
        {
            await BuildVisibilityFilterAsync(viewerUserName, ct)
        };

        if (!string.IsNullOrWhiteSpace(authorUserName))
        {
            filters.Add(Builders<PrayerModel>.Filter.Eq(p => p.AuthorUsername, authorUserName));
        }

        if (!string.IsNullOrWhiteSpace(categoryId))
        {
            filters.Add(Builders<PrayerModel>.Filter.AnyEq(p => p.CategoryIds, categoryId));
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(text), "i");
            filters.Add(Builders<PrayerModel>.Filter.Regex(p => p.Content, pattern));
        }

        var filter = Builders<PrayerModel>.Filter.And(filters);
        var sort = Builders<PrayerModel>.Sort.Descending(p => p.CreatedAt);

        var models = await _prayers
            .Find(filter)
            .Sort(sort)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(ct);

        return await ToListResponsesAsync(models, ct);
    }

    private static DateTime ResolveActivityAt(PrayerModel prayer)
    {
        if (prayer.Supporters.Count == 0)
        {
            return prayer.UpdatedAt;
        }

        var lastReaction = prayer.Supporters.Max(s => s.ReactedAt);
        return lastReaction > prayer.UpdatedAt ? lastReaction : prayer.UpdatedAt;
    }

    private async Task<List<ListPrayerRepositoryResponseDto>> ToListResponsesAsync(
        IReadOnlyCollection<PrayerModel> prayers, CancellationToken ct)
    {
        if (prayers.Count == 0)
        {
            return [];
        }

        var authors = await LoadProfilesAsync(prayers.Select(p => p.AuthorUsername), ct);
        var categories = await LoadCategoryMapAsync(prayers.SelectMany(p => p.CategoryIds), ct);

        var result = new List<ListPrayerRepositoryResponseDto>();
        foreach (var prayer in prayers)
        {
            if (!authors.TryGetValue(prayer.AuthorUsername, out var author))
            {
                continue;
            }

            var prayerCategories = ResolveCategories(prayer.CategoryIds, categories);
            result.Add(new ListPrayerRepositoryResponseDto(author, prayer, prayerCategories));
        }

        return result;
    }

    private async Task<bool> CanViewAsync(PrayerModel prayer, string? viewerUserName, CancellationToken ct)
    {
        if (prayer.IsPublic)
        {
            return true;
        }

        if (viewerUserName is null)
        {
            return false;
        }

        if (string.Equals(viewerUserName, prayer.AuthorUsername, StringComparison.Ordinal))
        {
            return true;
        }

        return await AreAcceptedFriendsAsync(prayer.AuthorUsername, viewerUserName, ct);
    }

    private async Task<FilterDefinition<PrayerModel>> BuildVisibilityFilterAsync(
        string? viewerUserName, CancellationToken ct)
    {
        var publicFilter = Builders<PrayerModel>.Filter.Eq(p => p.IsPublic, true);

        if (viewerUserName is null)
        {
            return publicFilter;
        }

        var friends = await GetAcceptedFriendUserNamesAsync(viewerUserName, ct);
        var visibleAuthors = friends.Append(viewerUserName).Distinct().ToList();

        return Builders<PrayerModel>.Filter.Or(
            publicFilter,
            Builders<PrayerModel>.Filter.In(p => p.AuthorUsername, visibleAuthors));
    }

    private async Task<ProfileModel?> FindAuthorAsync(string username, CancellationToken ct)
    {
        var filter = Builders<ProfileModel>.Filter.Eq(u => u.UserName, username);
        return await _users.Find(filter).FirstOrDefaultAsync(ct);
    }

    private async Task<Dictionary<string, ProfileModel>> LoadProfilesAsync(
        IEnumerable<string> userNames, CancellationToken ct)
    {
        var distinct = userNames.Distinct().ToList();
        if (distinct.Count == 0)
        {
            return new Dictionary<string, ProfileModel>();
        }

        var filter = Builders<ProfileModel>.Filter.In(u => u.UserName, distinct);
        var profiles = await _users.Find(filter).ToListAsync(ct);

        return profiles
            .GroupBy(p => p.UserName)
            .ToDictionary(g => g.Key, g => g.First());
    }

    private async Task<List<PrayerCategoryPartialDto>> LoadCategoriesAsync(
        IReadOnlyCollection<string> categoryIds, CancellationToken ct)
    {
        var map = await LoadCategoryMapAsync(categoryIds, ct);
        return ResolveCategories(categoryIds, map);
    }

    private async Task<Dictionary<string, PrayerCategoryPartialDto>> LoadCategoryMapAsync(
        IEnumerable<string> categoryIds, CancellationToken ct)
    {
        var validIds = categoryIds
            .Where(id => ObjectId.TryParse(id, out _))
            .Distinct()
            .ToList();

        if (validIds.Count == 0)
        {
            return new Dictionary<string, PrayerCategoryPartialDto>();
        }

        var filter = Builders<CategoryModel>.Filter.In(c => c.Id, validIds);
        var categories = await _categories.Find(filter).ToListAsync(ct);

        return categories.ToDictionary(
            c => c.Id!,
            c => new PrayerCategoryPartialDto(c.Id!, c.Name, c.ColorHex));
    }

    private static List<PrayerCategoryPartialDto> ResolveCategories(
        IReadOnlyCollection<string> categoryIds, IReadOnlyDictionary<string, PrayerCategoryPartialDto> map)
        => categoryIds
            .Where(map.ContainsKey)
            .Select(id => map[id])
            .ToList();

    private async Task<List<string>> GetAcceptedFriendUserNamesAsync(string userName, CancellationToken ct)
    {
        // Neo4j is a "pointer map" keyed by user id, so resolve the viewer's id from MongoDB,
        // traverse the accepted-friendship edges to collect friend ids, then resolve those ids
        // back to usernames from MongoDB (the source of truth for user details).
        var viewerId = await FindUserIdByUserNameAsync(userName, ct);
        if (viewerId is null)
        {
            return [];
        }

        var query = $$"""
            MATCH (viewer:User {id: $viewerId})-[f:{{DbEdges.Friendships}}]-(friend:User)
            WHERE f.acceptedAt IS NOT NULL
            RETURN DISTINCT friend.id AS id
            """;

        var friendIds = await _neo4j.ExecuteReadListAsync(
            query, new { viewerId }, r => r["id"].As<string?>(), ct);

        var validIds = friendIds
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .Distinct()
            .ToList();

        if (validIds.Count == 0)
        {
            return [];
        }

        var filter = Builders<ProfileModel>.Filter.In(u => u.Id, validIds);
        var friends = await _users.Find(filter).ToListAsync(ct);

        return friends
            .Select(profile => profile.UserName)
            .Where(name => !string.IsNullOrEmpty(name))
            .ToList();
    }

    private async Task<bool> AreAcceptedFriendsAsync(string ownerUserName, string viewerUserName, CancellationToken ct)
    {
        var ownerId = await FindUserIdByUserNameAsync(ownerUserName, ct);
        var viewerId = await FindUserIdByUserNameAsync(viewerUserName, ct);

        if (ownerId is null || viewerId is null)
        {
            return false;
        }

        var query = $$"""
            MATCH (owner:User {id: $ownerId})-[f:{{DbEdges.Friendships}}]-(viewer:User {id: $viewerId})
            WHERE f.acceptedAt IS NOT NULL
            RETURN COUNT(f) AS total
            """;

        return await _neo4j.ExecuteReadSingleAsync(
            query, new { ownerId, viewerId }, r => r["total"].As<int>() > 0, ct);
    }

    private async Task<string?> FindUserIdByUserNameAsync(string userName, CancellationToken ct)
    {
        var filter = Builders<ProfileModel>.Filter.Eq(u => u.UserName, userName);
        var profile = await _users.Find(filter).FirstOrDefaultAsync(ct);
        return profile?.Id;
    }
}
