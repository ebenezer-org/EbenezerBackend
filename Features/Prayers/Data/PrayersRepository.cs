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
        var prayerKey = ArangoDbUtils.ExtractKey(prayerId, CollectionName);

        if (prayerKey is null)
        {
            return null;
        }

        var query = $@"
            LET viewer = @viewerUserName == null || @viewerUserName == '' ? null : FIRST(
                FOR u IN {ArangoDbCollections.Users}
                    FILTER u.UserName == @viewerUserName
                    LIMIT 1
                    RETURN u
            )

            LET prayer = DOCUMENT({CollectionName}, @prayerKey)

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

        var deleteEdgesQuery = $@"
            FOR edge IN {ArangoDbEdges.CategorizedAs}
                FILTER edge._from == @prayerId
                REMOVE edge IN {ArangoDbEdges.CategorizedAs}
        ";
        
        await db.Cursor.PostCursorAsync<object>(
            deleteEdgesQuery, 
            new Dictionary<string, object> { { "prayerId", formattedPrayerId } }, 
            token: ct);
        
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

            // Only ONE modification operation on CategorizedAs in this query
            LET newLinks = (
                FOR category IN categories
                    INSERT {{
                        _from: prayerToUpdate._id,
                        _to: category._id,
                        CreatedAt: DATE_ISO8601(DATE_NOW())
                    }} INTO {ArangoDbEdges.CategorizedAs}
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
        var query = $@"
            LET viewer = FIRST(
                FOR u IN {ArangoDbCollections.Users}
                    FILTER u.UserName == @viewerUserName
                    LIMIT 1
                    RETURN u
            )
            FILTER viewer != null

            LET connectionCandidates = (
                FOR friend IN 1..1 ANY viewer._id {ArangoDbEdges.Friendships}
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
                    FILTER LENGTH(
                        FOR reactor IN 1..1 INBOUND myPrayer._id {ArangoDbEdges.ReactedBy}
                            LIMIT 1
                            RETURN 1
                    ) > 0
                    RETURN {{
                        Author: viewer,
                        Prayer: myPrayer,
                        IsOwn: true
                    }}
            )

            FOR item IN UNION(connectionCandidates, ownCandidates)
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

                LET reactions = item.IsOwn ? (
                    FOR reaction IN 1..1 INBOUND item.Prayer._id {ArangoDbEdges.ReactedBy}
                        LET reactor = DOCUMENT(reaction._from)

                        RETURN {{
                            Reactor: reactor,
                            CreatedAt: reaction.CreatedAt
                        }}
                ) : []

                RETURN MERGE(
                    {{
                        AuthorProfileModel: item.Author,
                        PrayerModel: item.Prayer,
                        Categories: categories,
                        Interactions: reactions
                    }},
                    item.IsOwn ? {{}} : {{ ActivityAt: item.ActivityAt }}
                )
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
        var hasAuthor = !string.IsNullOrEmpty(authorUserName);
        var hasCategory = !string.IsNullOrEmpty(categoryId);
        var hasText = !string.IsNullOrEmpty(text);

        var categoryKey = hasCategory
            ? ArangoDbUtils.ExtractKey(categoryId, ArangoDbCollections.Categories)
            : null;

        if (hasCategory && categoryKey is null)
        {
            return [];
        }

        var offset = Math.Max(0, (page - 1) * pageSize);

        var bindVars = new Dictionary<string, object>
        {
            { "viewerUserName", viewerUserName ?? string.Empty },
            { "offset", offset },
            { "pageSize", pageSize }
        };

        var viewerLet = $@"
            LET viewer = @viewerUserName == null || @viewerUserName == '' ? null : FIRST(
                FOR u IN {ArangoDbCollections.Users}
                    FILTER u.UserName == @viewerUserName
                    LIMIT 1
                    RETURN u
            )";

        var textFilter = string.Empty;
        if (hasText)
        {
            textFilter = "FILTER CONTAINS(LOWER(prayer.Content), LOWER(@text))";
            bindVars["text"] = text!;
        }

        var categoryHeader = string.Empty;
        if (hasCategory)
        {
            categoryHeader = $@"
            LET categoryDoc = DOCUMENT({ArangoDbCollections.Categories}, @categoryKey)
            FILTER categoryDoc != null
            FILTER categoryDoc.IsPublic == true || (viewer != null && categoryDoc.OwnerUsername == @viewerUserName)";
            bindVars["categoryKey"] = categoryKey!;
        }

        var categoriesProjection = $@"
                LET categories = (
                    FOR category IN 1..1 OUTBOUND prayer._id {ArangoDbEdges.CategorizedAs}
                        FILTER category.IsPublic == true || (viewer != null && category.OwnerUsername == @viewerUserName)
                        RETURN {{
                            Id: category._key,
                            Name: category.Name,
                            ColorHex: category.ColorHex
                        }}
                )";

        string query;

        if (hasAuthor)
        {
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

                FOR prayer IN 1..1 OUTBOUND author._id {ArangoDbEdges.PostedBy}
                    FILTER prayer.IsPublic == true || (viewer != null && author._id == viewer._id)
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
            query = $@"
            {viewerLet}
            {categoryHeader}

            FOR prayer IN 1..1 INBOUND categoryDoc._id {ArangoDbEdges.CategorizedAs}
                {textFilter}
                FILTER prayer.IsPublic == true || viewer != null

                LET author = FIRST(
                    FOR user IN 1..1 INBOUND prayer._id {ArangoDbEdges.PostedBy}
                        LIMIT 1
                        RETURN user
                )

                FILTER author != null
                FILTER prayer.IsPublic == true || (viewer != null && author._id == viewer._id)

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
            bindVars["fetchLimit"] = offset + pageSize;

            query = $@"
            {viewerLet}

            LET publicPrayers = (
                FOR prayer IN {CollectionName}
                    FILTER prayer.IsPublic == true
                    {textFilter}
                    SORT prayer.CreatedAt DESC
                    LIMIT @fetchLimit
                    RETURN prayer
            )

            LET ownPrivatePrayers = viewer == null ? [] : (
                FOR prayer IN 1..1 OUTBOUND viewer._id {ArangoDbEdges.PostedBy}
                    FILTER prayer.IsPublic != true
                    {textFilter}
                    SORT prayer.CreatedAt DESC
                    LIMIT @fetchLimit
                    RETURN prayer
            )

            FOR prayer IN APPEND(publicPrayers, ownPrivatePrayers)
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
