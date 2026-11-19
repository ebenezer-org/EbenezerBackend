using System.Text.RegularExpressions;
using EbenezerBackend.Features.Categories.Data.Models;
using EbenezerBackend.Features.Comments.Data.Models;
using EbenezerBackend.Features.Comments.Domain.Enums;
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
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Neo4j.Driver;

namespace EbenezerBackend.Features.Prayers.Data;

public class PrayersRepository : BaseRepository<PrayerModel>, IPrayersRepository
{
    private const string SupportInteractionType = "Support";

    private readonly IMongoCollection<PrayerModel> _prayers;
    private readonly IMongoCollection<ProfileModel> _users;
    private readonly IMongoCollection<CategoryModel> _categories;
    private readonly IMongoCollection<CommentsModel> _comments;
    private readonly INeo4JExecutor _neo4j;

    public PrayersRepository(IMongoDatabase database, INeo4JExecutor neo4j)
    {
        _comments = database.GetCollection<CommentsModel>(DbCollections.Comments);
        _prayers = database.GetCollection<PrayerModel>(CollectionName);
        _users = database.GetCollection<ProfileModel>(DbCollections.Users);
        _categories = database.GetCollection<CategoryModel>(DbCollections.Categories);
        _neo4j = neo4j;
    }

    public async Task<InsertPrayerResponseDto> InsertPrayerAsync(InsertPrayerRequestDto request, CancellationToken ct)
    {
        // Mesma regra do ArangoDB: autor e categorias são validados antes de gravar
        // (categoria pública ou do próprio autor); qualquer falha vira "não encontrado".
        var author = await FindAuthorAsync(request.AuthorUsername, ct)
                     ?? throw new NotFoundException("Usuário ou categoria não encontrados");

        var categoryModels = await ResolveUsableCategoriesAsync(
                                 request.CategoryIds ?? [], request.AuthorUsername, ownedOnly: false, ct)
                             ?? throw new NotFoundException("Usuário ou categoria não encontrados");

        var model = PrayerModel.FromEntity(request.PrayerEntity);
        model.Id = null;
        model.AuthorUsername = request.AuthorUsername;
        model.CategoryIds = categoryModels.Select(c => c.Id!).Distinct().ToList();
        model.Supporters = [];

        await _prayers.InsertOneAsync(model, cancellationToken: ct);

        return new InsertPrayerResponseDto(author, model, ToCategoryDtos(categoryModels));
    }

    public async Task<IReadOnlyCollection<ListPrayerRepositoryResponseDto>> ListPrayersAsync(
        string? viewerUserName, CancellationToken ct)
    {
        // Sem usuário autenticado nada é visível: oração pública só é vista pelo autor e seus amigos.
        if (string.IsNullOrEmpty(viewerUserName))
        {
            return [];
        }

        var filter = await BuildVisibilityFilterAsync(viewerUserName, ct);
        var sort = Builders<PrayerModel>.Sort.Descending(p => p.CreatedAt);

        var models = await _prayers.Find(filter).Sort(sort).ToListAsync(ct);

        return await ToListResponsesAsync(models, viewerUserName, ct);
    }

    public async Task<ListPrayerRepositoryResponseDto?> FindPrayerByIdAsync(
        string prayerId, string? viewerUserName, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(viewerUserName) || !ObjectId.TryParse(prayerId, out _))
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

        var categories = await LoadCategoriesAsync(model.CategoryIds, viewerUserName, ct);
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

        // Mesma regra do ArangoDB: na atualização, só categorias do próprio dono.
        var categoryModels = await ResolveUsableCategoriesAsync(categoryIds ?? [], ownerUserName, ownedOnly: true, ct);
        if (categoryModels is null)
        {
            return null;
        }

        var filter = Builders<PrayerModel>.Filter.And(
            Builders<PrayerModel>.Filter.Eq(p => p.Id, prayerId),
            Builders<PrayerModel>.Filter.Eq(p => p.AuthorUsername, ownerUserName));

        var update = Builders<PrayerModel>.Update
            .Set(p => p.Content, content)
            .Set(p => p.IsPublic, isPublic)
            .Set(p => p.CategoryIds, categoryModels.Select(c => c.Id!).Distinct().ToList())
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

        return new ListPrayerRepositoryResponseDto(author, model, ToCategoryDtos(categoryModels));
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

        if (result.DeletedCount == 0)
        {
            return false;
        }

        // Equivalente à remoção das arestas CommentedOn no ArangoDB: os comentários diretos da oração
        // deixam de ser acessíveis (exclusão lógica, como no restante dos comentários).
        var commentsFilter = Builders<CommentsModel>.Filter.And(
            Builders<CommentsModel>.Filter.Eq(c => c.ParentId, prayerId),
            Builders<CommentsModel>.Filter.Eq(c => c.ParentType, CommentParentTypeEnum.Prayer),
            Builders<CommentsModel>.Filter.Eq(c => c.DeletedAt, null));

        await _comments.UpdateManyAsync(
            commentsFilter,
            Builders<CommentsModel>.Update.Set(c => c.DeletedAt, DateTime.UtcNow),
            cancellationToken: ct);

        return true;
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

        var categories = await LoadCategoriesAsync(model.CategoryIds, null, ct);
        return new ListPrayerRepositoryResponseDto(author, model, categories);
    }

    public async Task AddSupportReactionAsync(string prayerId, string reactorUserName, CancellationToken ct)
    {
        if (!ObjectId.TryParse(prayerId, out _))
        {
            return;
        }

        // Mesma regra do ArangoDB: só em oração pública, e idempotente (uma reação já existente
        // não é duplicada nem reordenada). Uma única operação atômica no lugar de Pull + Push.
        var filter = Builders<PrayerModel>.Filter.And(
            Builders<PrayerModel>.Filter.Eq(p => p.Id, prayerId),
            Builders<PrayerModel>.Filter.Eq(p => p.IsPublic, true),
            Builders<PrayerModel>.Filter.Not(
                Builders<PrayerModel>.Filter.ElemMatch(p => p.Supporters, s => s.UserName == reactorUserName)));

        var push = Builders<PrayerModel>.Update.Push(
            p => p.Supporters,
            new PrayerSupportReactionModel { UserName = reactorUserName, ReactedAt = DateTime.UtcNow });

        await _prayers.UpdateOneAsync(filter, push, cancellationToken: ct);
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
        // Regras da timeline (iguais às do ArangoDB):
        //  - orações PÚBLICAS de amigos (amizade aceita); orações privadas de outros nunca aparecem;
        //  - orações do próprio usuário, só quando houve atualização (ao menos uma reação de apoio).
        var friends = await GetAcceptedFriendUserNamesAsync(viewerUserName, ct);

        var filter = Builders<PrayerModel>.Filter.Or(
            Builders<PrayerModel>.Filter.And(
                Builders<PrayerModel>.Filter.In(p => p.AuthorUsername, friends),
                Builders<PrayerModel>.Filter.Eq(p => p.IsPublic, true)),
            Builders<PrayerModel>.Filter.And(
                Builders<PrayerModel>.Filter.Eq(p => p.AuthorUsername, viewerUserName),
                Builders<PrayerModel>.Filter.SizeGt(p => p.Supporters, 0)));

        // Total e página vêm do MongoDB: só as orações da página saem do servidor.
        var totalCount = (int)await _prayers.CountDocumentsAsync(filter, cancellationToken: ct);
        if (totalCount == 0)
        {
            return ([], 0);
        }

        var pageItems = await FindTimelinePageAsync(
            filter, viewerUserName, Math.Max(0, (page - 1) * pageSize), pageSize, ct);

        if (pageItems.Count == 0)
        {
            return ([], totalCount);
        }

        var authorUserNames = pageItems.Select(p => p.AuthorUsername);
        var supporterUserNames = pageItems
            .Where(p => IsOwn(p, viewerUserName))
            .SelectMany(p => p.Supporters.Select(s => s.UserName));
        var profiles = await LoadProfilesAsync(authorUserNames.Concat(supporterUserNames), ct);

        var categoryMap = await LoadCategoryModelsAsync(pageItems.SelectMany(p => p.CategoryIds), ct);

        var items = new List<TimelinePrayerRepositoryResponseDto>();
        foreach (var prayer in pageItems)
        {
            if (!profiles.TryGetValue(prayer.AuthorUsername, out var author))
            {
                continue;
            }

            var isOwn = IsOwn(prayer, viewerUserName);
            var prayerCategories = ResolveCategories(prayer.CategoryIds, categoryMap, isOwn ? null : viewerUserName);

            // Apenas as orações do próprio usuário trazem as interações recebidas.
            var interactions = isOwn
                ? prayer.Supporters
                    .OrderBy(s => s.ReactedAt)
                    .Where(s => profiles.ContainsKey(s.UserName))
                    .Select(s =>
                    {
                        var reactor = profiles[s.UserName];
                        return new PrayerInteractionPartialDto(
                            SupportInteractionType, reactor.Id ?? string.Empty, reactor.UserName, reactor.FullName, s.ReactedAt);
                    })
                    .ToList()
                : new List<PrayerInteractionPartialDto>();

            items.Add(new TimelinePrayerRepositoryResponseDto(
                author, prayer, prayerCategories, interactions, ResolveActivityAt(prayer, viewerUserName)));
        }

        return (items, totalCount);
    }

    public async Task<IReadOnlyCollection<ListPrayerRepositoryResponseDto>> SearchPrayersAsync(
        string? viewerUserName, string? authorUserName, string? categoryId, string? text,
        int page, int pageSize, CancellationToken ct)
    {
        // Sem usuário autenticado nada é visível.
        if (string.IsNullOrEmpty(viewerUserName))
        {
            return [];
        }

        if (!string.IsNullOrWhiteSpace(categoryId)
            && (!ObjectId.TryParse(categoryId, out _) || !await IsCategoryVisibleAsync(categoryId, viewerUserName, ct)))
        {
            // Categoria inexistente ou não visível para o viewer (igual ao ArangoDB): nenhum resultado.
            return [];
        }

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

        return await ToListResponsesAsync(models, viewerUserName, ct);
    }

    private static bool IsOwn(PrayerModel prayer, string viewerUserName)
        => string.Equals(prayer.AuthorUsername, viewerUserName, StringComparison.Ordinal);

    // Amigo: data de criação. Oração própria com interação: data da última reação.
    private static DateTime ResolveActivityAt(PrayerModel prayer, string viewerUserName)
        => IsOwn(prayer, viewerUserName) && prayer.Supporters.Count > 0
            ? prayer.Supporters.Max(s => s.ReactedAt)
            : prayer.CreatedAt;

    private const string ActivityAtField = "ActivityAt";

    // Pipeline da timeline: filtra, calcula a data de atividade, ordena e pagina no servidor.
    // Ordem: ActivityAt desc (mesma regra de ResolveActivityAt), _id desc como desempate.
    private async Task<List<PrayerModel>> FindTimelinePageAsync(
        FilterDefinition<PrayerModel> filter, string viewerUserName, int offset, int pageSize, CancellationToken ct)
    {
        var renderArgs = new RenderArgs<PrayerModel>(_prayers.DocumentSerializer, BsonSerializer.SerializerRegistry);

        var pipeline = new BsonDocumentStagePipelineDefinition<PrayerModel, PrayerModel>(
            new[]
            {
                new BsonDocument("$match", filter.Render(renderArgs)),
                new BsonDocument("$addFields", new BsonDocument(ActivityAtField, TimelineActivityExpression(viewerUserName))),
                new BsonDocument("$sort", new BsonDocument { { ActivityAtField, -1 }, { "_id", -1 } }),
                new BsonDocument("$skip", offset),
                new BsonDocument("$limit", pageSize)
            },
            _prayers.DocumentSerializer);

        var cursor = await _prayers.AggregateAsync(pipeline, new AggregateOptions { AllowDiskUse = true }, ct);

        return await cursor.ToListAsync(ct);
    }

    private static BsonDocument TimelineActivityExpression(string viewerUserName) => new("$cond", new BsonArray
    {
        new BsonDocument("$and", new BsonArray
        {
            new BsonDocument("$eq", new BsonArray { "$" + nameof(PrayerModel.AuthorUsername), viewerUserName }),
            new BsonDocument("$gt", new BsonArray
            {
                new BsonDocument("$size", new BsonDocument("$ifNull",
                    new BsonArray { "$" + nameof(PrayerModel.Supporters), new BsonArray() })),
                0
            })
        }),
        new BsonDocument("$max", "$" + nameof(PrayerModel.Supporters) + "." + nameof(PrayerSupportReactionModel.ReactedAt)),
        "$" + nameof(PrayerModel.CreatedAt)
    });

    private async Task<List<ListPrayerRepositoryResponseDto>> ToListResponsesAsync(
        IReadOnlyCollection<PrayerModel> prayers, string viewerUserName, CancellationToken ct)
    {
        if (prayers.Count == 0)
        {
            return [];
        }

        var authors = await LoadProfilesAsync(prayers.Select(p => p.AuthorUsername), ct);
        var categoryMap = await LoadCategoryModelsAsync(prayers.SelectMany(p => p.CategoryIds), ct);

        var result = new List<ListPrayerRepositoryResponseDto>();
        foreach (var prayer in prayers)
        {
            if (!authors.TryGetValue(prayer.AuthorUsername, out var author))
            {
                continue;
            }

            var prayerCategories = ResolveCategories(prayer.CategoryIds, categoryMap, viewerUserName);
            result.Add(new ListPrayerRepositoryResponseDto(author, prayer, prayerCategories));
        }

        return result;
    }

    // Privada: só o autor. Pública: o autor e os amigos (amizade aceita) dele.
    private async Task<bool> CanViewAsync(PrayerModel prayer, string viewerUserName, CancellationToken ct)
    {
        if (IsOwn(prayer, viewerUserName))
        {
            return true;
        }

        return prayer.IsPublic && await AreAcceptedFriendsAsync(prayer.AuthorUsername, viewerUserName, ct);
    }

    private async Task<FilterDefinition<PrayerModel>> BuildVisibilityFilterAsync(
        string viewerUserName, CancellationToken ct)
    {
        var friends = await GetAcceptedFriendUserNamesAsync(viewerUserName, ct);

        return Builders<PrayerModel>.Filter.Or(
            Builders<PrayerModel>.Filter.Eq(p => p.AuthorUsername, viewerUserName),
            Builders<PrayerModel>.Filter.And(
                Builders<PrayerModel>.Filter.Eq(p => p.IsPublic, true),
                Builders<PrayerModel>.Filter.In(p => p.AuthorUsername, friends)));
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

    // Valida as categorias informadas antes de gravar (igual ao ArangoDB): todas precisam existir e
    // ser públicas ou do próprio usuário (ownedOnly: só do próprio usuário). Devolve null se alguma
    // falhar; a contagem compara com o total informado, inclusive repetidos.
    private async Task<List<CategoryModel>?> ResolveUsableCategoriesAsync(
        IEnumerable<string> rawCategoryIds, string userName, bool ownedOnly, CancellationToken ct)
    {
        var requested = rawCategoryIds.ToList();
        if (requested.Count == 0)
        {
            return [];
        }

        var validIds = requested
            .Where(id => ObjectId.TryParse(id, out _))
            .Distinct()
            .ToList();

        if (validIds.Count == 0)
        {
            return null;
        }

        var ownedByUser = Builders<CategoryModel>.Filter.Eq(c => c.OwnerUsername, userName);
        var usable = ownedOnly
            ? ownedByUser
            : Builders<CategoryModel>.Filter.Or(
                Builders<CategoryModel>.Filter.Eq(c => c.IsPublic, true),
                ownedByUser);

        var filter = Builders<CategoryModel>.Filter.And(
            Builders<CategoryModel>.Filter.In(c => c.Id, validIds),
            usable);

        var found = await _categories.Find(filter).ToListAsync(ct);

        return found.Count == requested.Count ? found : null;
    }

    private async Task<bool> IsCategoryVisibleAsync(string categoryId, string viewerUserName, CancellationToken ct)
    {
        var filter = Builders<CategoryModel>.Filter.And(
            Builders<CategoryModel>.Filter.Eq(c => c.Id, categoryId),
            Builders<CategoryModel>.Filter.Or(
                Builders<CategoryModel>.Filter.Eq(c => c.IsPublic, true),
                Builders<CategoryModel>.Filter.Eq(c => c.OwnerUsername, viewerUserName)));

        return await _categories.Find(filter).AnyAsync(ct);
    }

    // visibleTo = null: sem restrição (dono vendo as próprias orações).
    private async Task<List<PrayerCategoryPartialDto>> LoadCategoriesAsync(
        IReadOnlyCollection<string> categoryIds, string? visibleTo, CancellationToken ct)
    {
        var map = await LoadCategoryModelsAsync(categoryIds, ct);
        return ResolveCategories(categoryIds, map, visibleTo);
    }

    private async Task<Dictionary<string, CategoryModel>> LoadCategoryModelsAsync(
        IEnumerable<string> categoryIds, CancellationToken ct)
    {
        var validIds = categoryIds
            .Where(id => ObjectId.TryParse(id, out _))
            .Distinct()
            .ToList();

        if (validIds.Count == 0)
        {
            return new Dictionary<string, CategoryModel>();
        }

        var filter = Builders<CategoryModel>.Filter.In(c => c.Id, validIds);
        var categories = await _categories.Find(filter).ToListAsync(ct);

        return categories
            .Where(c => c.Id is not null)
            .ToDictionary(c => c.Id!, c => c);
    }

    // Só expõe categorias públicas ou do próprio usuário que está vendo (igual ao ArangoDB).
    private static List<PrayerCategoryPartialDto> ResolveCategories(
        IReadOnlyCollection<string> categoryIds,
        IReadOnlyDictionary<string, CategoryModel> map,
        string? visibleTo)
        => categoryIds
            .Where(map.ContainsKey)
            .Select(id => map[id])
            .Where(c => visibleTo is null || c.IsPublic || string.Equals(c.OwnerUsername, visibleTo, StringComparison.Ordinal))
            .Select(c => new PrayerCategoryPartialDto(c.Id!, c.Name, c.ColorHex))
            .ToList();

    private static List<PrayerCategoryPartialDto> ToCategoryDtos(IEnumerable<CategoryModel> categories)
        => categories.Select(c => new PrayerCategoryPartialDto(c.Id!, c.Name, c.ColorHex)).ToList();

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
