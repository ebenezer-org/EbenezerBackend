using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArangoDBNetStandard;
using ArangoDBNetStandard.CursorApi.Models;
using EbenezerBackend.Features.Prayers.Data.Models;
using EbenezerBackend.Features.Prayers.Domain.Repositories;
using EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.Insert;
using EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.List;
using EbenezerBackend.Features.Prayers.Domain.Repositories.Dtos.Timeline;
using EbenezerBackend.Shared;
using EbenezerBackend.Shared.Exceptions;

namespace EbenezerBackend.Features.Prayers.Data;

public class PrayersRepository(IArangoDBClient db) : BaseRepository<PrayerModel>, IPrayersRepository
{
    private const string UsersCollectionName = "Users";
    private const string CategoriesCollectionName = "Categories";
    private const string PostedByCollectionName = "PostedBy";
    private const string CategorizedAsCollectionName = "CategorizedAs";
    private const string InteractsWithCollectionName = "InteractsWith";
    private const string FriendshipsCollectionName = "Friendships";
    private const string ReactedByCollectionName = "ReactedBy";

    public async Task<InsertPrayerResponseDto> InsertPrayerAsync(InsertPrayerRequestDto request, CancellationToken ct)
    {
        var query = $@"
            LET user = FIRST(
                FOR u IN {UsersCollectionName}
                    FILTER u.UserName == @userName
                    LIMIT 1
                    RETURN u
            )

            FILTER user != null

            LET categories = LENGTH(@categoryIds) == 0 ? [] : (
                FOR currentCategory IN {CategoriesCollectionName}
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
            }} INTO {PostedByCollectionName}

            LET categoryLinks = (
                FOR category IN categories
                    INSERT {{
                        _from: newPrayer._id,
                        _to: category._id,
                        CreatedAt: DATE_ISO8601(DATE_NOW())
                    }} INTO {CategorizedAsCollectionName}
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
                FOR u IN {UsersCollectionName}
                    FILTER u.UserName == @viewerUserName
                    LIMIT 1
                    RETURN u
            )

            FOR prayer IN {CollectionName}
                LET author = FIRST(
                    FOR user IN 1..1 INBOUND prayer._id {PostedByCollectionName}
                        LIMIT 1
                        RETURN user
                )

                FILTER author != null
                FILTER prayer.IsPublic == true || (viewer != null && author._id == viewer._id)

                LET categories = (
                    FOR category IN 1..1 OUTBOUND prayer._id {CategorizedAsCollectionName}
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
                FOR u IN {UsersCollectionName}
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
                FOR user IN 1..1 INBOUND prayer._id {PostedByCollectionName}
                    LIMIT 1
                    RETURN user
            )

            FILTER author != null
            FILTER prayer.IsPublic == true || (viewer != null && author._id == viewer._id)

            LET categories = (
                FOR category IN 1..1 OUTBOUND prayer._id {CategorizedAsCollectionName}
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
                FOR u IN {UsersCollectionName}
                    FILTER u.UserName == @userName
                    LIMIT 1
                    RETURN u
            )

            FILTER user != null

            LET prayerToUpdate = FIRST(
                FOR currentPrayer IN {CollectionName}
                    FILTER currentPrayer._key == @prayerId || currentPrayer._id == @prayerId
                    LET ownerLink = FIRST(
                        FOR edge IN {PostedByCollectionName}
                            FILTER edge._from == user._id && edge._to == currentPrayer._id
                            LIMIT 1
                            RETURN edge
                    )
                    FILTER ownerLink != null
                    RETURN currentPrayer
            )

            FILTER prayerToUpdate != null

            LET categories = LENGTH(@categoryIds) == 0 ? [] : (
                FOR currentCategory IN {CategoriesCollectionName}
                    FILTER currentCategory._key IN @categoryIds || currentCategory._id IN @categoryIds
                    FILTER currentCategory.IsPublic == true || currentCategory.OwnerUsername == @userName
                    RETURN currentCategory
            )

            FILTER LENGTH(@categoryIds) == 0 || LENGTH(categories) == LENGTH(@categoryIds)

            UPDATE prayerToUpdate WITH @prayer IN {CollectionName}
            LET updatedPrayer = NEW

            LET removedLinks = (
                FOR edge IN {CategorizedAsCollectionName}
                    FILTER edge._from == updatedPrayer._id
                    REMOVE edge IN {CategorizedAsCollectionName}
                    RETURN OLD
            )

            LET newLinks = (
                FOR category IN categories
                    INSERT {{
                        _from: updatedPrayer._id,
                        _to: category._id,
                        CreatedAt: DATE_ISO8601(DATE_NOW())
                    }} INTO {CategorizedAsCollectionName}
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
                FOR u IN {UsersCollectionName}
                    FILTER u.UserName == @userName
                    LIMIT 1
                    RETURN u
            )

            FILTER user != null

            LET prayerToDelete = FIRST(
                FOR currentPrayer IN {CollectionName}
                    FILTER currentPrayer._key == @prayerId || currentPrayer._id == @prayerId
                    LET ownerLink = FIRST(
                        FOR edge IN {PostedByCollectionName}
                            FILTER edge._from == user._id && edge._to == currentPrayer._id
                            LIMIT 1
                            RETURN edge
                    )
                    FILTER ownerLink != null
                    RETURN currentPrayer
            )

            FILTER prayerToDelete != null

            LET removedOwnership = (
                FOR edge IN {PostedByCollectionName}
                    FILTER edge._to == prayerToDelete._id
                    REMOVE edge IN {PostedByCollectionName}
                    RETURN OLD
            )

            LET removedCategories = (
                FOR edge IN {CategorizedAsCollectionName}
                    FILTER edge._from == prayerToDelete._id
                    REMOVE edge IN {CategorizedAsCollectionName}
                    RETURN OLD
            )

            LET removedInteractions = (
                FOR edge IN {InteractsWithCollectionName}
                    FILTER edge._to == prayerToDelete._id
                    REMOVE edge IN {InteractsWithCollectionName}
                    RETURN OLD
            )

            REMOVE prayerToDelete IN {CollectionName}
            RETURN 1
        ";

        var bindVars = new Dictionary<string, object>
        {
            { "userName", ownerUserName },
            { "prayerId", prayerId }
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
                FOR u IN {UsersCollectionName}
                    FILTER u.UserName == @userName
                    LIMIT 1
                    RETURN u
            )

            FILTER user != null

            LET prayerToUpdate = FIRST(
                FOR currentPrayer IN {CollectionName}
                    FILTER currentPrayer._key == @prayerId || currentPrayer._id == @prayerId
                    LET ownerLink = FIRST(
                        FOR edge IN {PostedByCollectionName}
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
                FOR category IN 1..1 OUTBOUND updatedPrayer._id {CategorizedAsCollectionName}
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

    public async Task<TimelinePrayerRepositoryResponseDto?> ToggleSupportReactionAsync(
        string prayerId,
        string reactorUserName,
        CancellationToken ct)
    {
        var query = $@"
            LET user = FIRST(
                FOR u IN {UsersCollectionName}
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

            LET author = FIRST(
                FOR u IN 1..1 INBOUND prayer._id {PostedByCollectionName}
                    LIMIT 1
                    RETURN u
            )

            FILTER author != null
            FILTER prayer.IsPublic == true
            FILTER author.UserName != @userName

            LET reaction = FIRST(
                FOR edge IN {ReactedByCollectionName}
                    FILTER edge._from == user._id && edge._to == prayer._id
                    LIMIT 1
                    RETURN edge
            )

            IF reaction != null THEN
                REMOVE reaction IN {ReactedByCollectionName}
            ELSE
                INSERT {{
                    _from: user._id,
                    _to: prayer._id,
                    CreatedAt: DATE_ISO8601(DATE_NOW())
                }} INTO {ReactedByCollectionName}

            RETURN {{
                AuthorProfileModel: user,
                PrayerModel: prayer,
                Categories: [],
                Interactions: [],
                ActivityAt: reaction.CreatedAt
            }}
        ";

        var bindVars = new Dictionary<string, object>
        {
            { "userName", reactorUserName },
            { "prayerId", prayerId }
        };

        var response = await db.Cursor.PostCursorAsync<TimelinePrayerRepositoryResponseDto>(query, bindVars, token: ct);

        return response.Result.FirstOrDefault();
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
                FOR u IN {UsersCollectionName}
                    FILTER u.UserName == @viewerUserName
                    LIMIT 1
                    RETURN u
            )
            FILTER viewer != null
    
            // 2. Busca Orações dos Amigos (OTIMIZADO)
            LET connectionPrayers = (
                FOR friend IN 1..1 ANY viewer._id {FriendshipsCollectionName}
                    // MÁGICA AQUI: Vai direto do amigo para as orações dele (O(1) para cada amigo)
                    FOR prayer IN 1..1 OUTBOUND friend._id {PostedByCollectionName}
                        FILTER prayer.IsPublic == true
                        
                        LET categories = (
                            FOR category IN 1..1 OUTBOUND prayer._id {CategorizedAsCollectionName}
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
                FOR myPrayer IN 1..1 OUTBOUND viewer._id {PostedByCollectionName}
                    
                    // Usamos grafo para buscar quem interagiu!
                    LET interactions = (
                        FOR reactor, edge IN 1..1 INBOUND myPrayer._id {InteractsWithCollectionName}
                            RETURN {{
                                Type: edge.Type,
                                UserName: reactor.UserName,
                                FullName: reactor.FullName,
                                CreatedAt: edge.CreatedAt
                            }}
                    )
    
                    FILTER LENGTH(interactions) > 0
    
                    LET categories = (
                        FOR category IN 1..1 OUTBOUND myPrayer._id {CategorizedAsCollectionName}
                            RETURN {{
                                Id: category._key,
                                Name: category.Name,
                                ColorHex: category.ColorHex
                            }}
                    )
    
                    LET latestInteraction = MAX(interactions[*].CreatedAt)
    
                    RETURN {{
                        AuthorProfileModel: viewer,
                        PrayerModel: myPrayer,
                        Categories: categories,
                        Interactions: interactions,
                        ActivityAt: latestInteraction
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
                FOR u IN {UsersCollectionName}
                    FILTER u.UserName == @viewerUserName
                    LIMIT 1
                    RETURN u
            )

            FOR prayer IN {CollectionName}
                LET author = FIRST(
                    FOR user IN 1..1 INBOUND prayer._id {PostedByCollectionName}
                        LIMIT 1
                        RETURN user
                )

                FILTER author != null
                FILTER prayer.IsPublic == true || (viewer != null && author._id == viewer._id)
                FILTER @authorUserName == null || @authorUserName == '' || author.UserName == @authorUserName
                FILTER @text == null || @text == '' || CONTAINS(LOWER(prayer.Content), LOWER(@text))

                LET categories = (
                    FOR category IN 1..1 OUTBOUND prayer._id {CategorizedAsCollectionName}
                        FILTER category.IsPublic == true || (viewer != null && category.OwnerUsername == @viewerUserName)
                        RETURN {{
                            Id: category._key,
                            Name: category.Name,
                            ColorHex: category.ColorHex
                        }}
                )

                FILTER @categoryId == null || @categoryId == '' || LENGTH(
                    FOR category IN 1..1 OUTBOUND prayer._id {CategorizedAsCollectionName}
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
