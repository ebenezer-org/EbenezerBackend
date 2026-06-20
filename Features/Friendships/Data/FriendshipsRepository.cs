using EbenezerBackend.Features.Friendships.Data.Models;
using EbenezerBackend.Features.Friendships.Domain.Entities;
using EbenezerBackend.Features.Friendships.Domain.Enums;
using EbenezerBackend.Features.Friendships.Domain.Repositories;
using EbenezerBackend.Features.Friendships.Domain.Repositories.Dtos;
using EbenezerBackend.Features.Profile.Data.Models;
using EbenezerBackend.Infrastructure.Data;
using EbenezerBackend.Shared.Data;
using MongoDB.Driver;
using Neo4j.Driver;

namespace EbenezerBackend.Features.Friendships.Data;

// Polyglot "pointer map" model: Neo4j stores only the relationships and the user node ids
// (lightweight pointers). All human-readable user details (userName, fullName) are the
// source-of-truth in MongoDB and are hydrated here after the graph traversal returns ids.
public class FriendshipsRepository(INeo4JExecutor executor, IMongoDatabase database)
    : BaseRepository<FriendshipEdgeModel>, IFriendshipsRepository
{
    private readonly IMongoCollection<ProfileModel> _users =
        database.GetCollection<ProfileModel>(DbCollections.Users);

    // Raw projection of a FRIENDSHIP edge as stored in Neo4j (ids + edge dates only).
    private sealed record FriendshipEdgeRaw(
        string Id, string FromId, string ToId, DateTime RequestedAt, DateTime? AcceptedAt);

    private sealed record SuggestionRaw(string CandidateId, int MutualFriendsCount, string MutualId);

    public async Task<(IReadOnlyCollection<FriendshipRequestEntity> Items, int TotalCount)> SearchFriendships(
        CancellationToken ct,
        int page,
        int pageSize,
        string? fromUserId = null,
        string? toUserId = null,
        (string? UserA, string? UserB)? containsUserIds = null,
        FriendshipStatusEnum? status = null)
    {
        var offset = (page - 1) * pageSize;

        bool? isPending = status switch
        {
            FriendshipStatusEnum.Pending  => true,
            FriendshipStatusEnum.Accepted => false,
            _                             => null
        };

        var parameters = new
        {
            fromUserId,
            toUserId,
            containsUserAId = containsUserIds?.UserA,
            containsUserBId = containsUserIds?.UserB,
            isPending,
            offset,
            pageSize
        };

        var dataQuery = $$"""
            MATCH (a:User)-[f:{{DbEdges.Friendships}}]->(b:User)
            WHERE
              ($fromUserId      IS NULL OR a.id = $fromUserId) AND
              ($toUserId        IS NULL OR b.id = $toUserId) AND
              ($containsUserAId IS NULL OR a.id = $containsUserAId OR b.id = $containsUserAId) AND
              ($containsUserBId IS NULL OR a.id = $containsUserBId OR b.id = $containsUserBId) AND
              ($isPending       IS NULL OR
                ($isPending = true  AND f.acceptedAt IS NULL) OR
                ($isPending = false AND f.acceptedAt IS NOT NULL))
            RETURN f.id AS id, a.id AS fromId, b.id AS toId,
                   f.requestedAt AS requestedAt, f.acceptedAt AS acceptedAt
            ORDER BY f.requestedAt DESC
            SKIP $offset LIMIT $pageSize
            """;

        var countQuery = $$"""
            MATCH (a:User)-[f:{{DbEdges.Friendships}}]->(b:User)
            WHERE
              ($fromUserId      IS NULL OR a.id = $fromUserId) AND
              ($toUserId        IS NULL OR b.id = $toUserId) AND
              ($containsUserAId IS NULL OR a.id = $containsUserAId OR b.id = $containsUserAId) AND
              ($containsUserBId IS NULL OR a.id = $containsUserBId OR b.id = $containsUserBId) AND
              ($isPending       IS NULL OR
                ($isPending = true  AND f.acceptedAt IS NULL) OR
                ($isPending = false AND f.acceptedAt IS NOT NULL))
            RETURN COUNT(*) AS total
            """;

        var edges = await executor.ExecuteReadListAsync(dataQuery, parameters, ReadEdge, ct);
        var profiles = await LoadProfilesAsync(edges.SelectMany(e => new[] { e.FromId, e.ToId }), ct);
        var items = edges.Select(e => ToEntity(e, profiles)).ToList();

        var total = await executor.ExecuteReadSingleAsync(
            countQuery, parameters, record => record["total"].As<int>(), ct);

        return (items, total);
    }

    public async Task<FriendshipRequestEntity> InsertFriendshipRequestAsync(
        string fromUserId, string toUserId, CancellationToken ct)
    {
        var friendshipId = Guid.NewGuid().ToString();
        var requestedAt = DateTime.UtcNow.ToString("O");

        // MERGE the user "pointer" nodes so the graph is self-healing: a node is created on demand
        // when it does not yet exist, while existing nodes (and their relationships) are reused.
        var query = $$"""
            MERGE (a:User {id: $fromUserId})
            MERGE (b:User {id: $toUserId})
            MERGE (a)-[f:{{DbEdges.Friendships}} {id: $friendshipId}]->(b)
            ON CREATE SET f.requestedAt = $requestedAt, f.acceptedAt = null
            RETURN f.id AS id, a.id AS fromId, b.id AS toId,
                   f.requestedAt AS requestedAt, f.acceptedAt AS acceptedAt
            """;

        var edge = await executor.ExecuteWriteSingleAsync(query, new
        {
            fromUserId,
            toUserId,
            friendshipId,
            requestedAt
        }, ReadEdge, ct);

        var profiles = await LoadProfilesAsync(new[] { edge.FromId, edge.ToId }, ct);
        return ToEntity(edge, profiles);
    }

    public async Task<FriendshipRequestEntity?> UpdateFriendshipRequestAsync(
        FriendshipRequestEntity entity, CancellationToken ct)
    {
        var query = $$"""
            MATCH (a:User)-[f:{{DbEdges.Friendships}} {id: $id}]->(b:User)
            SET f.acceptedAt = $acceptedAt
            RETURN f.id AS id, a.id AS fromId, b.id AS toId,
                   f.requestedAt AS requestedAt, f.acceptedAt AS acceptedAt
            """;

        var edge = await executor.ExecuteWriteSingleOrDefaultAsync(query, new
        {
            id = entity.Id,
            acceptedAt = Neo4JValueConverter.ToIso8601(entity.AcceptedAt)
        }, ReadEdge, ct);

        if (edge is null)
        {
            return null;
        }

        var profiles = await LoadProfilesAsync(new[] { edge.FromId, edge.ToId }, ct);
        return ToEntity(edge, profiles);
    }

    public async Task<FriendshipRequestEntity?> FindFriendshipRequestByIdAsync(
        string requestId, CancellationToken ct)
    {
        var query = $$"""
            MATCH (a:User)-[f:{{DbEdges.Friendships}} {id: $requestId}]->(b:User)
            RETURN f.id AS id, a.id AS fromId, b.id AS toId,
                   f.requestedAt AS requestedAt, f.acceptedAt AS acceptedAt
            """;

        var edges = await executor.ExecuteReadListAsync(query, new { requestId }, ReadEdge, ct);
        if (edges.Count == 0)
        {
            return null;
        }

        var edge = edges[0];
        var profiles = await LoadProfilesAsync(new[] { edge.FromId, edge.ToId }, ct);
        return ToEntity(edge, profiles);
    }

    public async Task<FriendshipRequestEntity?> DeleteFriendshipRequestByIdAsync(
        string requestId, CancellationToken ct)
    {
        var query = $$"""
            MATCH (a:User)-[f:{{DbEdges.Friendships}} {id: $requestId}]->(b:User)
            WITH a, b, f,
                 f.id          AS fId,
                 f.requestedAt AS rAt,
                 f.acceptedAt  AS aAt,
                 a.id          AS aId,
                 b.id          AS bId
            DELETE f
            RETURN fId AS id, aId AS fromId, bId AS toId,
                   rAt AS requestedAt, aAt AS acceptedAt
            """;

        var edge = await executor.ExecuteWriteSingleOrDefaultAsync(query, new { requestId }, ReadEdge, ct);
        if (edge is null)
        {
            return null;
        }

        var profiles = await LoadProfilesAsync(new[] { edge.FromId, edge.ToId }, ct);
        return ToEntity(edge, profiles);
    }

    public async Task<(IReadOnlyCollection<FriendSuggestionRepositoryDto> Items, int TotalCount)> GetFriendSuggestionsAsync(
        string userId, int depth, int page, int pageSize, CancellationToken ct)
    {
        var offset = (page - 1) * pageSize;
        const int minDepth = 2;
        var maxDepth = Math.Max(2, depth);

        var countQuery = $$$"""
            MATCH path = (me:User {id: $userId})-[:{{{DbEdges.Friendships}}}*{{{minDepth}}}..{{{maxDepth}}}]-(candidate:User)
            WHERE candidate.id <> $userId
              AND ALL(rel IN relationships(path) WHERE rel.acceptedAt IS NOT NULL)
              AND NOT EXISTS {
                  MATCH (me)-[direct:{{{DbEdges.Friendships}}}]-(candidate)
                  WHERE direct.acceptedAt IS NOT NULL
              }
            RETURN COUNT(DISTINCT candidate) AS total
            """;

        var dataQuery = $$$"""
            MATCH path = (me:User {id: $userId})-[:{{{DbEdges.Friendships}}}*{{{minDepth}}}..{{{maxDepth}}}]-(candidate:User)
            WHERE candidate.id <> $userId
              AND ALL(rel IN relationships(path) WHERE rel.acceptedAt IS NOT NULL)
              AND NOT EXISTS {
                  MATCH (me)-[direct:{{{DbEdges.Friendships}}}]-(candidate)
                  WHERE direct.acceptedAt IS NOT NULL
              }
            WITH DISTINCT candidate, me
            
            MATCH (me)-[r1:{{{DbEdges.Friendships}}}]-(mutual:User)-[r2:{{{DbEdges.Friendships}}}]-(candidate)
            WHERE r1.acceptedAt IS NOT NULL AND r2.acceptedAt IS NOT NULL
            
            WITH candidate, COUNT(DISTINCT mutual) AS mutualFriendsCount, COLLECT(DISTINCT mutual)[0] AS oldestMutualFriend
            ORDER BY mutualFriendsCount DESC
            SKIP $offset LIMIT $pageSize
            RETURN candidate.id           AS candidateId,
                   mutualFriendsCount,
                   oldestMutualFriend.id  AS mutualId
            """;

        var parameters = new { userId, offset, pageSize };

        var raws = await executor.ExecuteReadListAsync(dataQuery, parameters, record => new SuggestionRaw(
            CandidateId: record["candidateId"].As<string>(),
            MutualFriendsCount: record["mutualFriendsCount"].As<int>(),
            MutualId: record["mutualId"].As<string>()), ct);

        var profiles = await LoadProfilesAsync(
            raws.SelectMany(r => new[] { r.CandidateId, r.MutualId }), ct);

        var items = raws.Select(r => new FriendSuggestionRepositoryDto(
            FriendSuggestion: ToProfileDto(r.CandidateId, profiles),
            OldestMutualFriend: ToProfileDto(r.MutualId, profiles),
            MutualFriendsCount: r.MutualFriendsCount)).ToList();

        var total = await executor.ExecuteReadSingleAsync(
            countQuery, parameters, record => record["total"].As<int>(), ct);

        return (items, total);
    }

    public async Task<(IReadOnlyCollection<FriendProfileRepositoryDto> Items, int TotalCount)> GetMutualFriendsAsync(
        string userIdA, string userIdB, int page, int pageSize, CancellationToken ct)
    {
        var offset = (page - 1) * pageSize;

        var dataQuery = $$"""
            MATCH (a:User {id: $userIdA})-[:{{DbEdges.Friendships}}]-(mutual:User)-[:{{DbEdges.Friendships}}]-(b:User {id: $userIdB})
            RETURN DISTINCT mutual.id AS id
            """;

        var mutualIds = await executor.ExecuteReadListAsync(
            dataQuery, new { userIdA, userIdB }, record => record["id"].As<string>(), ct);

        var profiles = await LoadProfilesAsync(mutualIds, ct);

        var ordered = mutualIds
            .Select(id => ToProfileDto(id, profiles))
            .OrderBy(dto => dto.FullName, StringComparer.Ordinal)
            .ThenBy(dto => dto.UserName, StringComparer.Ordinal)
            .ThenBy(dto => dto.Id, StringComparer.Ordinal)
            .ToList();

        var total = ordered.Count;
        var items = ordered.Skip(offset).Take(pageSize).ToList();

        return (items, total);
    }

    private static FriendshipEdgeRaw ReadEdge(IRecord record) => new(
        Id: record["id"].As<string>(),
        FromId: record["fromId"].As<string>(),
        ToId: record["toId"].As<string>(),
        RequestedAt: Neo4JValueConverter.ToDateTime(record["requestedAt"]),
        AcceptedAt: Neo4JValueConverter.ToNullableDateTime(record["acceptedAt"]));

    private FriendshipRequestEntity ToEntity(
        FriendshipEdgeRaw edge, IReadOnlyDictionary<string, ProfileModel> profiles) => new(
        id: edge.Id,
        requesterId: edge.FromId,
        requesterUserName: UserNameOf(profiles, edge.FromId),
        requesterName: FullNameOf(profiles, edge.FromId),
        targetId: edge.ToId,
        targetUserName: UserNameOf(profiles, edge.ToId),
        targetName: FullNameOf(profiles, edge.ToId),
        requestedAt: edge.RequestedAt,
        acceptedAt: edge.AcceptedAt);

    private static FriendProfileRepositoryDto ToProfileDto(
        string id, IReadOnlyDictionary<string, ProfileModel> profiles) =>
        new(Id: id, FullName: FullNameOf(profiles, id), UserName: UserNameOf(profiles, id));

    private async Task<IReadOnlyDictionary<string, ProfileModel>> LoadProfilesAsync(
        IEnumerable<string> ids, CancellationToken ct)
    {
        var distinct = ids.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        if (distinct.Count == 0)
        {
            return new Dictionary<string, ProfileModel>();
        }

        var filter = Builders<ProfileModel>.Filter.In(p => p.Id, distinct);
        var profiles = await _users.Find(filter).ToListAsync(ct);

        return profiles
            .Where(p => p.Id is not null)
            .GroupBy(p => p.Id!)
            .ToDictionary(group => group.Key, group => group.First());
    }

    private static string UserNameOf(IReadOnlyDictionary<string, ProfileModel> profiles, string id) =>
        profiles.TryGetValue(id, out var profile) ? profile.UserName : string.Empty;

    // Falls back to the username when the user has no registered full name.
    private static string FullNameOf(IReadOnlyDictionary<string, ProfileModel> profiles, string id)
    {
        if (!profiles.TryGetValue(id, out var profile))
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(profile.FullName) ? profile.UserName : profile.FullName;
    }
}
