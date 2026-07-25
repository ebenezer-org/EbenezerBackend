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

            LET categories = LENGTH(@categoryIds) == 0 ? [] : (
                FOR currentCategory IN {ArangoDbCollections.Categories}
                    FILTER currentCategory._key IN @categoryIds || currentCategory._id IN @categoryIds
                    FILTER currentCategory.IsPublic == true || currentCategory.OwnerUsername == @userName
                    RETURN currentCategory
            )

            FILTER LENGTH(@categoryIds) == 0 || LENGTH(categories) == LENGTH(@categoryIds)

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
            { "categoryIds", request.CategoryIds ?? [] }
        };

        var response = await db.Cursor.PostCursorAsync<InsertPrayerResponseDto>(query, bindVars, token: ct);
        var result = response.Result.FirstOrDefault();

        return result ?? throw new NotFoundException("Usuário ou categoria não encontrados");
    }

    public async Task<IReadOnlyCollection<ListPrayerRepositoryResponseDto>> ListPrayersAsync(string? viewerUserName, CancellationToken ct)
    {
        var query = $@"
            LET viewer = @viewerUserName == null || @viewerUserName == '' ? null : FIRST(
                FOR u IN {ArangoDbCollections.Users}
                    FILTER u.UserName == @viewerUserName
                    LIMIT 1
                    RETURN u
            )

            FOR prayer IN {CollectionName}
                LET author = FIRST(
                    FOR user IN 1..1 INBOUND prayer._id {ArangoDbEdges.PostedBy}
                        LIMIT 1
                        RETURN user
                )

                FILTER author != null
                FILTER prayer.IsPublic == true || (viewer != null && author._id == viewer._id)

                LET categories = (
                    FOR category IN 1..1 OUTBOUND prayer._id {ArangoDbEdges.CategorizedAs}
                        FILTER category.IsPublic == true || (viewer != null && category.OwnerUsername == @viewerUserName)
                        RETURN {{
                            Id: category._key,
                            Name: category.Name,
                            ColorHex: category.ColorHex
                        }}
                )

                SORT prayer.CreatedAt DESC

                RETURN {{
                    AuthorProfileModel: author,
                    PrayerModel: prayer,
                    Categories: categories
                }}
        ";

        var bindVars = new Dictionary<string, object>
        {
            { "viewerUserName", viewerUserName ?? string.Empty }
        };

        var response = await db.Cursor.PostCursorAsync<ListPrayerRepositoryResponseDto>(query, bindVars, token: ct);

        return response.Result.ToList();
    }

    public async Task<ListPrayerRepositoryResponseDto?> FindPrayerByIdAsync(string prayerId, string? viewerUserName, CancellationToken ct)
    {
        var query = $@"
            LET viewer = @viewerUserName == null || @viewerUserName == '' ? null : FIRST(
                FOR u IN {ArangoDbCollections.Users}
                    FILTER u.UserName == @viewerUserName
                    LIMIT 1
                    RETURN u
            )

            LET prayer = FIRST(
                FOR currentPrayer IN {CollectionName}
                    FILTER currentPrayer._key == @prayerId || currentPrayer._id == @prayerId
                    LIMIT 1
                    RETURN currentPrayer
            )

            FILTER prayer != null

            LET author = FIRST(
                FOR user IN 1..1 INBOUND prayer._id {ArangoDbEdges.PostedBy}
                    LIMIT 1
                    RETURN user
            )

            FILTER author != null
            FILTER prayer.IsPublic == true || (viewer != null && author._id == viewer._id)

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
            { "prayerId", prayerId },
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
        var query = $@"
            LET user = FIRST(
                FOR u IN {ArangoDbCollections.Users}
                    FILTER u.UserName == @userName
                    LIMIT 1
                    RETURN u
            )

            FILTER user != null

            LET prayerToUpdate = FIRST(
                FOR currentPrayer IN {CollectionName}
                    FILTER currentPrayer._key == @prayerId || currentPrayer._id == @prayerId
                    LET ownerLink = FIRST(
                        FOR edge IN {ArangoDbEdges.PostedBy}
                            FILTER edge._from == user._id && edge._to == currentPrayer._id
                            LIMIT 1
                            RETURN edge
                    )
                    FILTER ownerLink != null
                    RETURN currentPrayer
            )

            FILTER prayerToUpdate != null

            LET categories = LENGTH(@categoryIds) == 0 ? [] : (
                FOR currentCategory IN {ArangoDbCollections.Categories}
                    FILTER currentCategory._key IN @categoryIds || currentCategory._id IN @categoryIds
                    FILTER currentCategory.IsPublic == true || currentCategory.OwnerUsername == @userName
                    RETURN currentCategory
            )

            FILTER LENGTH(@categoryIds) == 0 || LENGTH(categories) == LENGTH(@categoryIds)

            UPDATE prayerToUpdate WITH @prayer IN {CollectionName}
            LET updatedPrayer = NEW

            LET edgesToRemove = (
                FOR edge IN {ArangoDbEdges.CategorizedAs}
                    FILTER edge._from == updatedPrayer._id
                    RETURN edge._key
            )

            LET removedLinks = (
                FOR edgeKey IN edgesToRemove
                    REMOVE edgeKey IN {ArangoDbEdges.CategorizedAs}
                    RETURN OLD
            )

            LET newLinks = (
                FOR category IN categories
                    INSERT {{
                        _from: updatedPrayer._id,
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
                PrayerModel: updatedPrayer,
                Categories: categoriesResult
            }}
        ";

        var bindVars = new Dictionary<string, object>
        {
            { "userName", ownerUserName },
            { "prayerId", prayerId },
            { "categoryIds", categoryIds },
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

        var response = await db.Cursor.PostCursorAsync<ListPrayerRepositoryResponseDto>(query, bindVars, token: ct);

        return response.Result.FirstOrDefault();
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
        var query = $@"
            LET user = FIRST(
                FOR u IN {ArangoDbCollections.Users}
                    FILTER u.UserName == @userName
                    LIMIT 1
                    RETURN u
            )

            FILTER user != null

            LET prayerToUpdate = FIRST(
                FOR currentPrayer IN {CollectionName}
                    FILTER currentPrayer._key == @prayerId || currentPrayer._id == @prayerId
                    LET ownerLink = FIRST(
                        FOR edge IN {ArangoDbEdges.PostedBy}
                            FILTER edge._from == user._id && edge._to == currentPrayer._id
                            LIMIT 1
                            RETURN edge
                    )
                    FILTER ownerLink != null
                    RETURN currentPrayer
            )

            FILTER prayerToUpdate != null

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
            { "prayerId", prayerId },
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
        var query = $@"
            LET user = FIRST(
                FOR u IN {ArangoDbCollections.Users} 
                    FILTER u.UserName == @userName 
                    LIMIT 1 
                    RETURN u
            )
            
            LET prayer = FIRST(
                FOR currentPrayer IN {CollectionName} 
                    FILTER currentPrayer._key == @prayerId || currentPrayer._id == @prayerId 
                    LIMIT 1 
                    RETURN currentPrayer
            )

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
            { "prayerId", ArangoDbUtils.BuildArangoDbId(prayerId, CollectionName)! }
        };

        await db.Cursor.PostCursorAsync(query, bindVars, token: ct);
    }
        
    public async Task<(IReadOnlyCollection<TimelinePrayerRepositoryResponseDto>, int TotalCount)> GetTimelineAsync(
        string viewerUserName,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var query = $@"
            // 1. Pega o usuário logado
            LET viewer = FIRST(
                FOR u IN {ArangoDbCollections.Users}
                    FILTER u.UserName == @viewerUserName
                    LIMIT 1
                    RETURN u
            )
            FILTER viewer != null
    
            // 2. Busca Orações dos Amigos (OTIMIZADO)
            LET connectionPrayers = (
                FOR friend IN 1..1 ANY viewer._id {ArangoDbEdges.Friendships}
                    // MÁGICA AQUI: Vai direto do amigo para as orações dele (O(1) para cada amigo)
                    FOR prayer IN 1..1 OUTBOUND friend._id {ArangoDbEdges.PostedBy}
                        FILTER prayer.IsPublic == true
                        
                        LET categories = (
                            FOR category IN 1..1 OUTBOUND prayer._id {ArangoDbEdges.CategorizedAs}
                                FILTER category.IsPublic == true || category.OwnerUsername == @viewerUserName
                                RETURN {{
                                    Id: category._key,
                                    Name: category.Name,
                                    ColorHex: category.ColorHex
                                }}
                        )
    
                        RETURN {{
                            AuthorProfileModel: friend,
                            PrayerModel: prayer,
                            Categories: categories,
                            Interactions: [],
                            ActivityAt: prayer.CreatedAt
                        }}
            )
    
            // 3. Busca Interações nas PRÓPRIAS orações (OTIMIZADO)
            LET ownInteractions = (
                // MÁGICA AQUI: Pega direto as orações do usuário logado
                FOR myPrayer IN 1..1 OUTBOUND viewer._id {ArangoDbEdges.PostedBy}

                    LET reactions = (
                        FOR reaction IN 1..1 INBOUND myPrayer._id {ArangoDbEdges.ReactedBy}
                            LET reactor = DOCUMENT(reaction._from)

                            RETURN {{
                                Reactor: reactor,
                                CreatedAt: reaction.CreatedAt
                            }}
                    )
    
                    FILTER LENGTH(reactions) > 0
    
                    LET categories = (
                        FOR category IN 1..1 OUTBOUND myPrayer._id {ArangoDbEdges.CategorizedAs}
                            RETURN {{
                                Id: category._key,
                                Name: category.Name,
                                ColorHex: category.ColorHex
                            }}
                    )
    
                    RETURN {{
                        AuthorProfileModel: viewer,
                        PrayerModel: myPrayer,
                        Categories: categories,
                        Interactions: reactions,
                    }}
            )
    
            // 4. Junta tudo, ordena e pagina
            LET timeline = UNION(connectionPrayers, ownInteractions)
    
            FOR item IN timeline
                SORT item.ActivityAt DESC
                LIMIT @offset, @pageSize
                RETURN item
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
        var query = $@"
            LET viewer = @viewerUserName == null || @viewerUserName == '' ? null : FIRST(
                FOR u IN {ArangoDbCollections.Users}
                    FILTER u.UserName == @viewerUserName
                    LIMIT 1
                    RETURN u
            )

            FOR prayer IN {CollectionName}
                LET author = FIRST(
                    FOR user IN 1..1 INBOUND prayer._id {ArangoDbEdges.PostedBy}
                        LIMIT 1
                        RETURN user
                )

                FILTER author != null
                FILTER prayer.IsPublic == true || (viewer != null && author._id == viewer._id)
                FILTER @authorUserName == null || @authorUserName == '' || author.UserName == @authorUserName
                FILTER @text == null || @text == '' || CONTAINS(LOWER(prayer.Content), LOWER(@text))

                LET categories = (
                    FOR category IN 1..1 OUTBOUND prayer._id {ArangoDbEdges.CategorizedAs}
                        FILTER category.IsPublic == true || (viewer != null && category.OwnerUsername == @viewerUserName)
                        RETURN {{
                            Id: category._key,
                            Name: category.Name,
                            ColorHex: category.ColorHex
                        }}
                )

                FILTER @categoryId == null || @categoryId == '' || LENGTH(
                    FOR category IN 1..1 OUTBOUND prayer._id {ArangoDbEdges.CategorizedAs}
                        FILTER category._key == @categoryId || category._id == @categoryId
                        FILTER category.IsPublic == true || (viewer != null && category.OwnerUsername == @viewerUserName)
                        RETURN 1
                ) > 0

                SORT prayer.CreatedAt DESC
                LIMIT @offset, @pageSize

                RETURN {{
                    AuthorProfileModel: author,
                    PrayerModel: prayer,
                    Categories: categories
                }}
        ";

        var offset = Math.Max(0, (page - 1) * pageSize);

        var bindVars = new Dictionary<string, object>
        {
            { "viewerUserName", viewerUserName ?? string.Empty },
            { "authorUserName", authorUserName ?? string.Empty },
            { "categoryId", categoryId ?? string.Empty },
            { "text", text ?? string.Empty },
            { "offset", offset },
            { "pageSize", pageSize }
        };

        var response = await db.Cursor.PostCursorAsync<ListPrayerRepositoryResponseDto>(query, bindVars, token: ct);

        return response.Result.ToList();
    }
}
