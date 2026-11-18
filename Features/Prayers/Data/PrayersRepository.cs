using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArangoDBNetStandard;
using ArangoDBNetStandard.CursorApi.Models;
using EbenezerBackend.Features.Prayers.Data.Models;
using EbenezerBackend.Features.Prayers.Domain.Enums;
using EbenezerBackend.Features.Prayers.Domain.Repositories;
using EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.Insert;
using EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.List;
using EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.Timeline;
using EbenezerBackend.Shared;
using EbenezerBackend.Shared.Data;
using EbenezerBackend.Shared.Web.Exceptions;

namespace EbenezerBackend.Features.Prayers.Data;

public class PrayersRepository(IArangoDBClient db) : BaseRepository<PrayerModel>, IPrayersRepository
{
    public async Task<InsertPrayerResponseDto> InsertPrayerAsync(InsertPrayerRequestDto request, CancellationToken ct)
    {
        var query = $@"
            LET user = FIRST(
                FOR u IN {ArangoDbCollections.Users}
                    FILTER u.UserName == @userName
                    LIMIT 1
                    RETURN u
            )

            FILTER user != null

            LET categories = LENGTH(@categoryKeys) == 0 ? [] : (
                FOR categoryKey IN UNIQUE(@categoryKeys)
                    LET currentCategory = DOCUMENT({ArangoDbCollections.Categories}, categoryKey)
                    FILTER currentCategory != null
                    FILTER currentCategory.IsPublic == true || currentCategory.OwnerUsername == @userName
                    RETURN currentCategory
            )

            FILTER LENGTH(@categoryKeys) == 0 || LENGTH(categories) == LENGTH(@categoryKeys)

            INSERT @prayer INTO {CollectionName}
            LET newPrayer = NEW

            INSERT {{
                _from: user._id,
                _to: newPrayer._id,
                CreatedAt: DATE_ISO8601(DATE_NOW())
            }} INTO {ArangoDbEdges.PostedBy}

            LET categoryLinks = (
                FOR category IN categories
                    INSERT {{
                        _from: newPrayer._id,
                        _to: category._id,
                        CreatedAt: DATE_ISO8601(DATE_NOW())
                    }} INTO {ArangoDbEdges.CategorizedAs}
                    RETURN NEW
            )

            LET categoriesResult = (
                FOR category IN categories
                    RETURN {{
                        Id: category._key,
                        Name: category.Name,
                        ColorHex: category.ColorHex
                    }}
            )

            RETURN {{
                AuthorProfileModel: user,
                PrayerModel: newPrayer,
                Categories: categoriesResult
            }}
        ";

        var prayerModel = PrayerModel.FromEntity(request.PrayerEntity);

        var bindVars = new Dictionary<string, object>
        {
            { "prayer", prayerModel },
            { "userName", request.AuthorUsername },
            { "categoryKeys", ToCategoryKeys(request.CategoryIds) }
        };

        var response = await db.Cursor.PostCursorAsync<InsertPrayerResponseDto>(query, bindVars, token: ct);
        var result = response.Result.FirstOrDefault();

        return result ?? throw new NotFoundException("Usuário ou categoria não encontrados");
    }

    // Mesma regra de visibilidade da busca (um único ponto de verdade), sem filtros e sem paginação real.
    public Task<IReadOnlyCollection<ListPrayerRepositoryResponseDto>> ListPrayersAsync(string? viewerUserName, CancellationToken ct)
        => SearchPrayersAsync(viewerUserName, null, null, null, 1, int.MaxValue / 2, ct);

    public async Task<ListPrayerRepositoryResponseDto?> FindPrayerByIdAsync(string prayerId, string? viewerUserName, CancellationToken ct)
    {
        var prayerKey = ArangoDbUtils.ExtractKey(prayerId, CollectionName);

        // Sem usuário autenticado nada é visível: oração pública só é vista pelo autor e seus amigos.
        if (prayerKey is null || string.IsNullOrEmpty(viewerUserName))
        {
            return null;
        }

        var query = $@"
            LET viewer = FIRST(
                FOR u IN {ArangoDbCollections.Users}
                    FILTER u.UserName == @viewerUserName
                    LIMIT 1
                    RETURN u
            )

            FILTER viewer != null

            LET prayer = DOCUMENT({CollectionName}, @prayerKey)

            FILTER prayer != null

            LET author = FIRST(
                FOR user IN 1..1 INBOUND prayer._id {ArangoDbEdges.PostedBy}
                    LIMIT 1
                    RETURN user
            )

            FILTER author != null

            // Privada: só o autor. Pública: o autor e os amigos (amizade aceita) dele.
            LET isFriend = LENGTH(
                FOR friendship IN {ArangoDbEdges.Friendships}
                    FILTER friendship.AcceptedAt != null
                    FILTER (friendship._from == viewer._id AND friendship._to == author._id)
                        OR (friendship._from == author._id AND friendship._to == viewer._id)
                    LIMIT 1
                    RETURN 1
            ) > 0

            FILTER author._id == viewer._id || (prayer.IsPublic == true && isFriend)

            LET categories = (
                FOR category IN 1..1 OUTBOUND prayer._id {ArangoDbEdges.CategorizedAs}
                    FILTER category.IsPublic == true || (viewer != null && category.OwnerUsername == @viewerUserName)
                    RETURN {{
                        Id: category._key,
                        Name: category.Name,
                        ColorHex: category.ColorHex
                    }}
            )

            RETURN {{
                AuthorProfileModel: author,
                PrayerModel: prayer,
                Categories: categories
            }}
        ";

        var bindVars = new Dictionary<string, object>
        {
            { "prayerKey", prayerKey },
            { "viewerUserName", viewerUserName ?? string.Empty }
        };

        var response = await db.Cursor.PostCursorAsync<ListPrayerRepositoryResponseDto>(query, bindVars, token: ct);

        return response.Result.FirstOrDefault();
    }

    public async Task<ListPrayerRepositoryResponseDto?> UpdatePrayerAsync(
        string prayerId,
        string ownerUserName,
        string content,
        bool isPublic,
        IReadOnlyCollection<string> categoryIds,
        CancellationToken ct)
    {
        var formattedPrayerId = ArangoDbUtils.BuildArangoDbId(prayerId, CollectionName)!;

        var updateQuery = $@"
            LET user = FIRST(
                FOR u IN {ArangoDbCollections.Users}
                    FILTER u.UserName == @userName
                    LIMIT 1
                    RETURN u
            )

            FILTER user != null

            LET prayerToUpdate = DOCUMENT(@prayerId)
            FILTER prayerToUpdate != null

            LET ownerLink = FIRST(
                FOR edge IN {ArangoDbEdges.PostedBy}
                    FILTER edge._from == user._id && edge._to == prayerToUpdate._id
                    LIMIT 1
                    RETURN edge
            )

            FILTER ownerLink != null

            LET categories = LENGTH(@categoryKeys) == 0 ? [] : (
                FOR categoryKey IN UNIQUE(@categoryKeys)
                    LET currentCategory = DOCUMENT({ArangoDbCollections.Categories}, categoryKey)
                    FILTER currentCategory != null
                    FILTER currentCategory.OwnerUsername == @userName
                    RETURN currentCategory
            )

            FILTER LENGTH(@categoryKeys) == 0 || LENGTH(categories) == LENGTH(@categoryKeys)

            // Só chega aqui se o usuário é dono da oração e todas as categorias são válidas.
            // Remove apenas as arestas que deixaram de existir (só UMA modificação em CategorizedAs
            // por query); as novas são inseridas na query seguinte.
            LET keepCategoryIds = categories[*]._id
            LET removedLinks = (
                FOR link IN {ArangoDbEdges.CategorizedAs}
                    FILTER link._from == prayerToUpdate._id
                    FILTER link._to NOT IN keepCategoryIds
                    REMOVE link IN {ArangoDbEdges.CategorizedAs}
                    RETURN 1
            )

            LET categoriesResult = (
                FOR category IN categories
                    RETURN {{
                        Id: category._key,
                        Name: category.Name,
                        ColorHex: category.ColorHex
                    }}
            )

            UPDATE prayerToUpdate WITH @prayer IN {CollectionName}
            LET updatedPrayer = NEW

            RETURN {{
                AuthorProfileModel: user,
                PrayerModel: updatedPrayer,
                Categories: categoriesResult
            }}
        ";

        var bindVars = new Dictionary<string, object>
        {
            { "userName", ownerUserName },
            { "prayerId", formattedPrayerId },
            { "categoryKeys", ToCategoryKeys(categoryIds) },
            {
                "prayer",
                new Dictionary<string, object>
                {
                    { "Content", content },
                    { "IsPublic", isPublic },
                    { "UpdatedAt", DateTime.UtcNow }
                }
            }
        };

        var response = await db.Cursor.PostCursorAsync<ListPrayerRepositoryResponseDto>(updateQuery, bindVars, token: ct);

        var updated = response.Result.FirstOrDefault();

        // Dono inválido, oração inexistente ou categoria inválida: nada foi alterado.
        if (updated is null)
        {
            return null;
        }

        // Cria só as arestas que ainda não existem, para as categorias já validadas acima.
        if (updated.Categories.Count > 0)
        {
            var insertLinksQuery = $@"
                LET existing = (
                    FOR link IN {ArangoDbEdges.CategorizedAs}
                        FILTER link._from == @prayerId
                        RETURN link._to
                )

                FOR categoryId IN @categoryDbIds
                    FILTER categoryId NOT IN existing
                    INSERT {{
                        _from: @prayerId,
                        _to: categoryId,
                        CreatedAt: DATE_ISO8601(DATE_NOW())
                    }} INTO {ArangoDbEdges.CategorizedAs}
            ";

            var categoryDbIds = updated.Categories
                .Select(category => ArangoDbUtils.BuildArangoDbId(category.Id, ArangoDbCollections.Categories)!)
                .ToList();

            await db.Cursor.PostCursorAsync<object>(
                insertLinksQuery,
                new Dictionary<string, object>
                {
                    { "prayerId", formattedPrayerId },
                    { "categoryDbIds", categoryDbIds }
                },
                token: ct);
        }

        return updated;
    }

    public async Task<bool> DeletePrayerAsync(string prayerId, string ownerUserName, CancellationToken ct)
    {
        var query = $@"
            LET user = FIRST(
                FOR u IN {ArangoDbCollections.Users}
                    FILTER u.UserName == @userName
                    LIMIT 1
                    RETURN u
            )

            FILTER user != null

            LET prayerToDelete = DOCUMENT(@prayerId)
            FILTER prayerToDelete != null

            LET ownerLink = FIRST(
                FOR edge IN {ArangoDbEdges.PostedBy}
                    FILTER edge._from == user._id && edge._to == prayerToDelete._id
                    LIMIT 1
                    RETURN edge
            )

            FILTER ownerLink != null

            LET removedOwnership = (
                FOR edge IN {ArangoDbEdges.PostedBy}
                    FILTER edge._to == prayerToDelete._id
                    REMOVE edge IN {ArangoDbEdges.PostedBy}
                    RETURN OLD
            )

            LET removedCategories = (
                FOR edge IN {ArangoDbEdges.CategorizedAs}
                    FILTER edge._from == prayerToDelete._id
                    REMOVE edge IN {ArangoDbEdges.CategorizedAs}
                    RETURN OLD
            )

            LET removedReactions = (
                FOR edge IN {ArangoDbEdges.ReactedBy}
                    FILTER edge._to == prayerToDelete._id
                    REMOVE edge IN {ArangoDbEdges.ReactedBy}
                    RETURN OLD
            )

            LET removedComments = (
                FOR edge IN {ArangoDbEdges.CommentedOn}
                    FILTER edge._to == prayerToDelete._id
                    REMOVE edge IN {ArangoDbEdges.CommentedOn}
                    RETURN OLD
            )

            REMOVE prayerToDelete IN {CollectionName}
            RETURN 1
        ";

        var bindVars = new Dictionary<string, object>
        {
            { "userName", ownerUserName },
            { "prayerId", ArangoDbUtils.BuildArangoDbId(prayerId, CollectionName)! }
        };

        var response = await db.Cursor.PostCursorAsync<int>(query, bindVars, token: ct);

        return response.Result.Any();
    }

    public async Task<ListPrayerRepositoryResponseDto?> SetAuthorResponseAsync(
        string prayerId,
        string ownerUserName,
        string? message,
        CancellationToken ct)
    {
        var prayerKey = ArangoDbUtils.ExtractKey(prayerId, CollectionName);

        if (prayerKey is null)
        {
            return null;
        }

        var query = $@"
            LET user = FIRST(
                FOR u IN {ArangoDbCollections.Users}
                    FILTER u.UserName == @userName
                    LIMIT 1
                    RETURN u
            )

            FILTER user != null

            LET prayerToUpdate = DOCUMENT({CollectionName}, @prayerKey)

            FILTER prayerToUpdate != null

            LET ownerLink = FIRST(
                FOR edge IN {ArangoDbEdges.PostedBy}
                    FILTER edge._from == user._id && edge._to == prayerToUpdate._id
                    LIMIT 1
                    RETURN edge
            )

            FILTER ownerLink != null

            UPDATE prayerToUpdate WITH {{
                AuthorResponseMessage: @message,
                AuthorResponseCreatedAt: DATE_ISO8601(DATE_NOW()),
                UpdatedAt: DATE_ISO8601(DATE_NOW())
            }} IN {CollectionName}
            LET updatedPrayer = NEW

            LET categories = (
                FOR category IN 1..1 OUTBOUND updatedPrayer._id {ArangoDbEdges.CategorizedAs}
                    RETURN {{
                        Id: category._key,
                        Name: category.Name,
                        ColorHex: category.ColorHex
                    }}
            )

            RETURN {{
                AuthorProfileModel: user,
                PrayerModel: updatedPrayer,
                Categories: categories
            }}
        ";

        var bindVars = new Dictionary<string, object>
        {
            { "userName", ownerUserName },
            { "prayerKey", prayerKey },
            { "message", message ?? string.Empty }
        };

        var response = await db.Cursor.PostCursorAsync<ListPrayerRepositoryResponseDto>(query, bindVars, token: ct);

        return response.Result.FirstOrDefault();
    }

    public async Task AddSupportReactionAsync(
        string prayerId,
        string reactorUserName,
        CancellationToken ct)
    {
        var query = $@"
            LET user = FIRST(
                FOR u IN {ArangoDbCollections.Users} 
                    FILTER u.UserName == @userName 
                    LIMIT 1 
                    RETURN u
            )
            
            LET prayer = DOCUMENT(@prayerId)

            FILTER user != null && prayer != null

            LET author = FIRST(
                FOR u IN 1..1 INBOUND prayer._id {ArangoDbEdges.PostedBy} 
                    LIMIT 1 
                    RETURN u
            )

            FILTER author != null && prayer.IsPublic == true

            LET existingReaction = FIRST(
                FOR edge IN {ArangoDbEdges.ReactedBy}
                    FILTER edge._from == user._id && edge._to == prayer._id
                    LIMIT 1
                    RETURN edge
            )

            LET newReaction = (
                FOR i IN existingReaction == null ? [1] : []
                    INSERT {{
                        _from: user._id,
                        _to: prayer._id,
                        CreatedAt: DATE_ISO8601(DATE_NOW())
                    }} INTO {ArangoDbEdges.ReactedBy}
                    RETURN NEW
            )

            RETURN true
        ";

        var bindVars = new Dictionary<string, object>
        {
            { "userName", reactorUserName },
            { "prayerId", ArangoDbUtils.BuildArangoDbId(prayerId, CollectionName)! }
        };

        await db.Cursor.PostCursorAsync(query, bindVars, token: ct);
    }

    public async Task RemoveSupportReactionAsync(
        string prayerId,
        string reactorUserName,
        CancellationToken ct)
    {
        var prayerKey = ArangoDbUtils.ExtractKey(prayerId, CollectionName);

        if (prayerKey is null)
        {
            return;
        }

        var query = $@"
            LET user = FIRST(
                FOR u IN {ArangoDbCollections.Users} 
                    FILTER u.UserName == @userName 
                    LIMIT 1 
                    RETURN u
            )
            
            LET prayer = DOCUMENT({CollectionName}, @prayerKey)

            FILTER user != null && prayer != null

            LET removedReactions = (
                FOR edge IN {ArangoDbEdges.ReactedBy}
                    FILTER edge._from == user._id && edge._to == prayer._id
                    REMOVE edge IN {ArangoDbEdges.ReactedBy}
                    RETURN OLD
            )

            RETURN true
        ";

        var bindVars = new Dictionary<string, object>
        {
            { "userName", reactorUserName },
            { "prayerKey", prayerKey }
        };

        await db.Cursor.PostCursorAsync(query, bindVars, token: ct);
    }
        

    public async Task<(IReadOnlyCollection<TimelinePrayerRepositoryResponseDto>, int TotalCount)> GetTimelineAsync(
        string viewerUserName,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        // Regras da timeline:
        //  - orações PÚBLICAS de amigos (amizade aceita); orações privadas de outros nunca aparecem;
        //  - orações do próprio usuário, só quando houve atualização (ao menos uma reação de apoio).
        // Os candidatos carregam só o necessário para ordenar; categorias e reações são resolvidas
        // apenas para os itens da página, depois do LIMIT.
        var query = $@"
            LET viewer = FIRST(
                FOR u IN {ArangoDbCollections.Users}
                    FILTER u.UserName == @viewerUserName
                    LIMIT 1
                    RETURN u
            )
            FILTER viewer != null

            LET friendCandidates = (
                FOR friend, friendship IN 1..1 ANY viewer._id {ArangoDbEdges.Friendships}
                    FILTER friendship.AcceptedAt != null
                    FOR prayer IN 1..1 OUTBOUND friend._id {ArangoDbEdges.PostedBy}
                        FILTER prayer.IsPublic == true
                        RETURN {{
                            Author: friend,
                            Prayer: prayer,
                            ActivityAt: prayer.CreatedAt,
                            IsOwn: false
                        }}
            )

            LET ownCandidates = (
                FOR myPrayer IN 1..1 OUTBOUND viewer._id {ArangoDbEdges.PostedBy}
                    LET lastReactionAt = MAX(
                        FOR reactor, reaction IN 1..1 INBOUND myPrayer._id {ArangoDbEdges.ReactedBy}
                            RETURN reaction.CreatedAt
                    )
                    FILTER lastReactionAt != null
                    RETURN {{
                        Author: viewer,
                        Prayer: myPrayer,
                        ActivityAt: lastReactionAt,
                        IsOwn: true
                    }}
            )

            FOR item IN UNION(friendCandidates, ownCandidates)
                SORT item.ActivityAt DESC
                LIMIT @offset, @pageSize

                LET categories = (
                    FOR category IN 1..1 OUTBOUND item.Prayer._id {ArangoDbEdges.CategorizedAs}
                        FILTER item.IsOwn OR category.IsPublic == true OR category.OwnerUsername == @viewerUserName
                        RETURN {{
                            Id: category._key,
                            Name: category.Name,
                            ColorHex: category.ColorHex
                        }}
                )

                LET interactions = item.IsOwn ? (
                    FOR reactor, reaction IN 1..1 INBOUND item.Prayer._id {ArangoDbEdges.ReactedBy}
                        SORT reaction.CreatedAt ASC
                        RETURN {{
                            Type: ""Support"",
                            Id: reactor._key,
                            UserName: reactor.UserName,
                            FullName: reactor.FullName,
                            CreatedAt: reaction.CreatedAt
                        }}
                ) : []

                RETURN {{
                    AuthorProfileModel: item.Author,
                    PrayerModel: item.Prayer,
                    Categories: categories,
                    Interactions: interactions,
                    ActivityAt: item.ActivityAt
                }}
        ";

        var offset = Math.Max(0, (page - 1) * pageSize);

        var bindVars = new Dictionary<string, object>
        {
            { "viewerUserName", viewerUserName },
            { "offset", offset },
            { "pageSize", pageSize }
        };

        var postCursorBody = new PostCursorBody()
        {
            Query = query,
            BindVars = bindVars,
            Options = new PostCursorOptions
            {
                FullCount = true,
            }
        };

        var response = await db.Cursor.PostCursorAsync<TimelinePrayerRepositoryResponseDto>(postCursorBody, token: ct);

        var totalCount = (int)(response.Extra?.Stats?.FullCount ?? 0);

        return (response.Result.ToList(), totalCount);
    }

    public async Task<IReadOnlyCollection<ListPrayerRepositoryResponseDto>> SearchPrayersAsync(
        string? viewerUserName,
        string? authorUserName,
        string? categoryId,
        string? text,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        // Visibilidade: privada só para o autor; pública para o autor e seus amigos (amizade aceita).
        // Sem usuário autenticado nada é visível.
        if (string.IsNullOrEmpty(viewerUserName))
        {
            return [];
        }

        var hasAuthor = !string.IsNullOrEmpty(authorUserName);
        var hasCategory = !string.IsNullOrEmpty(categoryId);
        var hasText = !string.IsNullOrEmpty(text);

        var categoryKey = hasCategory
            ? ArangoDbUtils.ExtractKey(categoryId, ArangoDbCollections.Categories)
            : null;

        if (hasCategory && categoryKey is null)
        {
            // Id de outra coleção: nenhuma categoria possível.
            return [];
        }

        var offset = Math.Max(0, (page - 1) * pageSize);

        var bindVars = new Dictionary<string, object>
        {
            { "viewerUserName", viewerUserName },
            { "offset", offset },
            { "pageSize", pageSize }
        };

        var viewerLet = $@"
            LET viewer = FIRST(
                FOR u IN {ArangoDbCollections.Users}
                    FILTER u.UserName == @viewerUserName
                    LIMIT 1
                    RETURN u
            )

            FILTER viewer != null

            LET friendIds = (
                FOR friend, friendship IN 1..1 ANY viewer._id {ArangoDbEdges.Friendships}
                    FILTER friendship.AcceptedAt != null
                    RETURN friend._id
            )";

        var textFilter = string.Empty;
        if (hasText)
        {
            textFilter = "FILTER CONTAINS(LOWER(prayer.Content), LOWER(@text))";
            bindVars["text"] = text!;
        }

        // A categoria é resolvida uma vez por lookup direto e o resultado é podado cedo
        // quando ela não existe ou não é visível para o viewer.
        var categoryHeader = string.Empty;
        if (hasCategory)
        {
            categoryHeader = $@"
            LET categoryDoc = DOCUMENT({ArangoDbCollections.Categories}, @categoryKey)
            FILTER categoryDoc != null
            FILTER categoryDoc.IsPublic == true || categoryDoc.OwnerUsername == @viewerUserName";
            bindVars["categoryKey"] = categoryKey!;
        }

        var categoriesProjection = $@"
                LET categories = (
                    FOR category IN 1..1 OUTBOUND prayer._id {ArangoDbEdges.CategorizedAs}
                        FILTER category.IsPublic == true || category.OwnerUsername == @viewerUserName
                        RETURN {{
                            Id: category._key,
                            Name: category.Name,
                            ColorHex: category.ColorHex
                        }}
                )";

        string query;

        if (hasAuthor)
        {
            // Parte do autor (índice único de UserName) e percorre só as orações dele.
            bindVars["authorUserName"] = authorUserName!;

            var categoryEdgeFilter = hasCategory
                ? $@"FILTER LENGTH(
                        FOR categoryEdge IN {ArangoDbEdges.CategorizedAs}
                            FILTER categoryEdge._from == prayer._id AND categoryEdge._to == categoryDoc._id
                            LIMIT 1
                            RETURN 1
                    ) > 0"
                : string.Empty;

            query = $@"
            {viewerLet}
            {categoryHeader}

            FOR author IN {ArangoDbCollections.Users}
                FILTER author.UserName == @authorUserName
                LIMIT 1

                LET isOwn = author._id == viewer._id
                FILTER isOwn OR author._id IN friendIds

                FOR prayer IN 1..1 OUTBOUND author._id {ArangoDbEdges.PostedBy}
                    FILTER isOwn OR prayer.IsPublic == true
                    {textFilter}
                    {categoryEdgeFilter}

                    SORT prayer.CreatedAt DESC
                    LIMIT @offset, @pageSize

                    {categoriesProjection}

                    RETURN {{
                        AuthorProfileModel: author,
                        PrayerModel: prayer,
                        Categories: categories
                    }}
            ";
        }
        else if (hasCategory)
        {
            // Parte da categoria e percorre só as orações vinculadas a ela.
            query = $@"
            {viewerLet}
            {categoryHeader}

            FOR prayer IN 1..1 INBOUND categoryDoc._id {ArangoDbEdges.CategorizedAs}
                {textFilter}

                LET author = FIRST(
                    FOR user IN 1..1 INBOUND prayer._id {ArangoDbEdges.PostedBy}
                        LIMIT 1
                        RETURN user
                )

                FILTER author != null
                FILTER author._id == viewer._id OR (prayer.IsPublic == true AND author._id IN friendIds)

                SORT prayer.CreatedAt DESC
                LIMIT @offset, @pageSize

                {categoriesProjection}

                RETURN {{
                    AuthorProfileModel: author,
                    PrayerModel: prayer,
                    Categories: categories
                }}
            ";
        }
        else
        {
            // Listagem geral: orações públicas dos amigos e todas as próprias, cada fonte já ordenada
            // e limitada a offset + pageSize. Só as orações da página têm autor e categorias resolvidos.
            bindVars["fetchLimit"] = offset + pageSize;

            query = $@"
            {viewerLet}

            LET friendPrayers = (
                FOR friendId IN friendIds
                    FOR prayer IN 1..1 OUTBOUND friendId {ArangoDbEdges.PostedBy}
                        FILTER prayer.IsPublic == true
                        {textFilter}
                        SORT prayer.CreatedAt DESC
                        LIMIT @fetchLimit
                        RETURN prayer
            )

            LET ownPrayers = (
                FOR prayer IN 1..1 OUTBOUND viewer._id {ArangoDbEdges.PostedBy}
                    {textFilter}
                    SORT prayer.CreatedAt DESC
                    LIMIT @fetchLimit
                    RETURN prayer
            )

            FOR prayer IN APPEND(friendPrayers, ownPrayers)
                SORT prayer.CreatedAt DESC
                LIMIT @offset, @pageSize

                LET author = FIRST(
                    FOR user IN 1..1 INBOUND prayer._id {ArangoDbEdges.PostedBy}
                        LIMIT 1
                        RETURN user
                )

                FILTER author != null

                {categoriesProjection}

                RETURN {{
                    AuthorProfileModel: author,
                    PrayerModel: prayer,
                    Categories: categories
                }}
            ";
        }

        var response = await db.Cursor.PostCursorAsync<ListPrayerRepositoryResponseDto>(query, bindVars, token: ct);

        return response.Result.ToList();
    }

    private static List<string> ToCategoryKeys(IEnumerable<string>? categoryIds)
        => (categoryIds ?? [])
            .Select(id => ArangoDbUtils.ExtractKey(id, ArangoDbCollections.Categories) ?? string.Empty)
            .ToList();
}
