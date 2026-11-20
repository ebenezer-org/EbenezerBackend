using EbenezerBackend.Features.Friendships.Data.Models;
using EbenezerBackend.Features.Friendships.Domain.Entities;
using EbenezerBackend.Features.Friendships.Domain.Enums;
using EbenezerBackend.Features.Friendships.Domain.Repositories;
using EbenezerBackend.Features.Friendships.Domain.Repositories.Dtos;
using EbenezerBackend.Features.Profile.Data.Models;
using EbenezerBackend.Infrastructure.Data;
using EbenezerBackend.Shared.Data;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Neo4j.Driver;

namespace EbenezerBackend.Features.Friendships.Data;

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

        // Âncora por igualdade em User.id: a constraint user_id_unique resolve o nó inicial por
        // NodeUniqueIndexSeek e o custo passa a depender só do grau do nó, não do tamanho da base.
        // O padrão anterior (`$p IS NULL OR a.id = $p`) não permite seek de índice.
        var userA = containsUserIds?.UserA;
        var userB = containsUserIds?.UserB;

        var anchorId = userA ?? userB ?? fromUserId ?? toUserId
            ?? throw new ArgumentException("SearchFriendships exige ao menos um id de usuário como âncora.");

        var conditions = new List<string>();
        string match;
        string? otherId = null;

        if (userA is not null || userB is not null)
        {
            // Relacionamentos do usuário em qualquer direção; com um segundo id, só os entre os dois.
            match = $"MATCH (x:User {{id: $anchorId}})-[f:{DbEdges.Friendships}]-(y:User)";
            if (userA is not null && userB is not null)
            {
                otherId = userB;
                conditions.Add("y.id = $otherId");
            }
        }
        else if (fromUserId is not null)
        {
            match = $"MATCH (x:User {{id: $anchorId}})-[f:{DbEdges.Friendships}]->(y:User)";
            if (toUserId is not null)
            {
                otherId = toUserId;
                conditions.Add("y.id = $otherId");
            }
        }
        else
        {
            match = $"MATCH (y:User)-[f:{DbEdges.Friendships}]->(x:User {{id: $anchorId}})";
        }

        if (status == FriendshipStatusEnum.Pending)
        {
            conditions.Add("f.acceptedAt IS NULL");
        }
        else if (status == FriendshipStatusEnum.Accepted)
        {
            conditions.Add("f.acceptedAt IS NOT NULL");
        }

        var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : string.Empty;

        // Uma única consulta devolve o total e a página (antes eram duas varreduras por chamada).
        var query = $$"""
            {{match}}
            {{whereClause}}
            WITH f ORDER BY f.requestedAt DESC
            WITH collect({
                id: f.id,
                fromId: startNode(f).id,
                toId: endNode(f).id,
                requestedAt: f.requestedAt,
                acceptedAt: f.acceptedAt
            }) AS rows
            RETURN size(rows) AS total, rows[$offset..($offset + $pageSize)] AS page
            """;

        var (edges, total) = await executor.ExecuteReadSingleAsync(
            query,
            new { anchorId, otherId, offset, pageSize },
            record => (
                Edges: ((IEnumerable<object>)record["page"])
                    .Cast<IDictionary<string, object>>()
                    .Select(ReadEdgeFromMap)
                    .ToList(),
                Total: record["total"].As<int>()),
            ct);

        var profiles = await LoadProfilesAsync(edges.SelectMany(e => new[] { e.FromId, e.ToId }), ct);
        var items = edges.Select(e => ToEntity(e, profiles)).ToList();

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
            MERGE (a)-[f:{{DbEdges.Friendships}}]->(b)
            ON CREATE SET f.id = $friendshipId, f.requestedAt = $requestedAt
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

        var profiles = await LoadProfilesAsync([edge.FromId, edge.ToId], ct);
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

        var profiles = await LoadProfilesAsync([edge.FromId, edge.ToId], ct);
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
        var profiles = await LoadProfilesAsync([edge.FromId, edge.ToId], ct);
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

        var profiles = await LoadProfilesAsync([edge.FromId, edge.ToId], ct);
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

            WITH candidate, mutual, r1
            ORDER BY r1.acceptedAt ASC

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
        var offset = Math.Max(0, (page - 1) * pageSize);

        var dataQuery = $$"""
            MATCH (a:User {id: $userIdA})-[r1:{{DbEdges.Friendships}}]-(mutual:User)-[r2:{{DbEdges.Friendships}}]-(b:User {id: $userIdB})
            WHERE r1.acceptedAt IS NOT NULL AND r2.acceptedAt IS NOT NULL
            RETURN DISTINCT mutual.id AS id
            """;

        var mutualIds = await executor.ExecuteReadListAsync(
            dataQuery, new { userIdA, userIdB }, record => record["id"].As<string>(), ct);

        if (mutualIds.Count == 0)
        {
            return ([], 0);
        }

        // Ordenação e paginação no MongoDB: só a página de perfis sai do servidor.
        var filter = Builders<ProfileModel>.Filter.In(p => p.Id, mutualIds);
        var total = (int)await _users.CountDocumentsAsync(filter, cancellationToken: ct);
        var profiles = await FindMutualFriendsPageAsync(filter, offset, pageSize, ct);

        return (profiles.Select(ToFriendProfileDto).ToList(), total);
    }

    private static FriendshipEdgeRaw ReadEdge(IRecord record) => new(
        Id: record["id"].As<string>(),
        FromId: record["fromId"].As<string>(),
        ToId: record["toId"].As<string>(),
        RequestedAt: Neo4JValueConverter.ToDateTime(record["requestedAt"]),
        AcceptedAt: Neo4JValueConverter.ToNullableDateTime(record["acceptedAt"]));

    // Mesma projeção, lida de um mapa devolvido dentro de uma lista (collect) da consulta de busca.
    private static FriendshipEdgeRaw ReadEdgeFromMap(IDictionary<string, object> row) => new(
        Id: row["id"].As<string>(),
        FromId: row["fromId"].As<string>(),
        ToId: row["toId"].As<string>(),
        RequestedAt: Neo4JValueConverter.ToDateTime(row["requestedAt"]),
        AcceptedAt: Neo4JValueConverter.ToNullableDateTime(row.TryGetValue("acceptedAt", out var accepted) ? accepted : null));

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

    // Mesmo nome exibido de FullNameOf: sem nome completo, vale o nome de usuário.
    private static FriendProfileRepositoryDto ToFriendProfileDto(ProfileModel profile) =>
        new(Id: profile.Id ?? string.Empty,
            FullName: string.IsNullOrWhiteSpace(profile.FullName) ? profile.UserName : profile.FullName,
            UserName: profile.UserName);

    private async Task<List<ProfileModel>> FindMutualFriendsPageAsync(
        FilterDefinition<ProfileModel> filter, int offset, int pageSize, CancellationToken ct)
    {
        var renderArgs = new RenderArgs<ProfileModel>(_users.DocumentSerializer, BsonSerializer.SerializerRegistry);

        var pipeline = new BsonDocumentStagePipelineDefinition<ProfileModel, ProfileModel>(
            new[]
            {
                new BsonDocument("$match", filter.Render(renderArgs)),
                new BsonDocument("$addFields", new BsonDocument(SortNameField, SortNameExpression())),
                new BsonDocument("$sort", new BsonDocument
                {
                    { SortNameField, 1 },
                    { nameof(ProfileModel.UserName), 1 },
                    { "_id", 1 }
                }),
                new BsonDocument("$skip", offset),
                new BsonDocument("$limit", pageSize)
            },
            _users.DocumentSerializer);

        var cursor = await _users.AggregateAsync(pipeline, new AggregateOptions { AllowDiskUse = true }, ct);

        return await cursor.ToListAsync(ct);
    }

    private const string SortNameField = "SortName";

    // Ordem por nome completo (ou nome de usuário, quando o completo está vazio), depois usuário e id.
    // $trim cobre só espaços ASCII; string.IsNullOrWhiteSpace também aceita espaços Unicode.
    private static BsonDocument SortNameExpression() => new("$cond", new BsonArray
    {
        new BsonDocument("$eq", new BsonArray
        {
            new BsonDocument("$trim", new BsonDocument("input",
                new BsonDocument("$ifNull", new BsonArray { "$" + nameof(ProfileModel.FullName), string.Empty }))),
            string.Empty
        }),
        "$" + nameof(ProfileModel.UserName),
        "$" + nameof(ProfileModel.FullName)
    });

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
