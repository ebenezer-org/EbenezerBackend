using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArangoDBNetStandard;
using ArangoDBNetStandard.CursorApi.Models;
using EbenezerBackend.Features.Friendships.Data.Models;
using EbenezerBackend.Features.Friendships.Domain.Entities;
using EbenezerBackend.Features.Friendships.Domain.Enums;
using EbenezerBackend.Features.Friendships.Domain.Repositories;
using EbenezerBackend.Features.Friendships.Domain.Repositories.Dtos;
using EbenezerBackend.Shared.Data;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EbenezerBackend.Features.Friendships.Data;

public class FriendshipsRepository(IArangoDBClient db) : BaseRepository<FriendshipEdgeModel>, IFriendshipsRepository
{
    public async Task<(IReadOnlyCollection<FriendshipRequestEntity> Items, int TotalCount)> SearchFriendships(
        CancellationToken ct,
        int page,
        int pageSize,
        string? fromUserId = null,
        string? toUserId = null,
        (string? UserA, string? UserB)? containsUserIds = null,
        FriendshipStatusEnum? status = null
    )
    {
        var offset = (page - 1) * pageSize;

        var query = $@"
        FOR edge IN {CollectionName}

            FILTER (@containsUserAId == null || (edge._from == @containsUserAId || edge._to == @containsUserAId))
            FILTER (@containsUserBId == null || (edge._from == @containsUserBId || edge._to == @containsUserBId))

            FILTER (@fromUserId == null || edge._from == @fromUserId)

            FILTER (@toUserId == null || edge._to == @toUserId)
            
            FILTER (@isPending == null || 
                (@isPending == true && edge.AcceptedAt == null) || 
                (@isPending == false && edge.AcceptedAt != null)
            )

            SORT edge.RequestedAt DESC
            LIMIT @offset, @pageSize

            LET fromUser = DOCUMENT(edge._from)
            LET toUser = DOCUMENT(edge._to)

            RETURN {{
                _key: edge._key,
                _id: edge._id,
                _rev: edge._rev,
                _from: edge._from,
                _to: edge._to,
                FromUserName: fromUser.UserName,
                FromName: fromUser.FullName,
                ToUserName: toUser.UserName,
                ToName: toUser.FullName,
                RequestedAt: edge.RequestedAt,
                AcceptedAt: edge.AcceptedAt,
            }}
    ";

        bool? isPendingValue = status == null ? null : status == FriendshipStatusEnum.Pending;


        var postBody = new PostCursorBody
        {
            Query = query,
            BindVars = new Dictionary<string, object?>
            {
                { "fromUserId", ArangoDbUtils.BuildArangoDbId(fromUserId, ArangoDbCollections.Users) },
                { "toUserId", ArangoDbUtils.BuildArangoDbId(toUserId, ArangoDbCollections.Users) },
                { "containsUserAId", ArangoDbUtils.BuildArangoDbId(containsUserIds?.UserA, ArangoDbCollections.Users) },
                { "containsUserBId", ArangoDbUtils.BuildArangoDbId(containsUserIds?.UserB, ArangoDbCollections.Users) },
                { "isPending", isPendingValue },
                { "offset", offset },
                { "pageSize", pageSize }
            },
            Options = new PostCursorOptions
            {
                FullCount = true
            }
        };

        var response = await db.Cursor.PostCursorAsync<FriendshipEdgeModel>(postCursorBody: postBody, token: ct);

        var totalCount = (int)(response.Extra?.Stats?.FullCount ?? 0);

        return (response.Result
            .Select(model => model.ToEntity())
            .ToList(), totalCount);
    }

    public async Task<(IReadOnlyCollection<FriendSuggestionRepositoryDto> Items, int TotalCount)> GetFriendSuggestionsAsync(
        string userId, 
        int depth,
        int page, 
        int pageSize, 
        CancellationToken ct)
    {
        var offset = (page - 1) * pageSize;

        var query = $@"
            // 1. Blacklist: Quem já está conectado diretamente
            LET directConnections = (
                FOR directUser IN 1..1 ANY @userId {CollectionName}
                    RETURN directUser._id
            )

            FOR candidate, friendship, path IN 2..@depth ANY @userId {CollectionName}
                FILTER friendship.AcceptedAt != null 
                FILTER candidate._id != @userId               
                FILTER candidate._id NOT IN directConnections 

                // 2. AGRUPAMENTO: Agrupa pelo candidato e joga os amigos em comum + datas num grupo em memória
                COLLECT finalCandidate = candidate INTO mutualFriendsGroup = {{
                    mutualFriendProfile: path.vertices[1],
                    connectedAt: path.edges[0].AcceptedAt != null ? path.edges[0].AcceptedAt : path.edges[0].RequestedAt
                }}
                
                // 3. CÁLCULO DO TOTAL: Quantos amigos em comum existem no total para este candidato
                LET mutualFriendsCount = LENGTH(mutualFriendsGroup)

                // 4. SELEÇÃO DO MAIS ANTIGO: Ordena o grupo local por data ascendente (mais antiga) e pega o primeiro
                LET oldestFriend = (
                    FOR item IN mutualFriendsGroup
                        SORT item.connectedAt ASC
                        LIMIT 1
                        RETURN item.mutualFriendProfile
                )[0]

                // 5. ORDENAÇÃO E PAGINAÇÃO PRINCIPAL
                SORT mutualFriendsCount DESC
                LIMIT @offset, @pageSize

                RETURN {{
                    FriendSuggestion: {{
                        Id: finalCandidate._key,
                        FullName: finalCandidate.FullName,
                        UserName: finalCandidate.UserName
                    }},
                    OldestMutualFriend: {{
                        Id: oldestFriend._key,
                        FullName: oldestFriend.FullName,
                        UserName: oldestFriend.UserName
                    }},
                    MutualFriendsCount: mutualFriendsCount
                }}
        ";

        var postBody = new PostCursorBody
        {
            Query = query,
            BindVars = new Dictionary<string, object?>
            {
                { "userId", ArangoDbUtils.BuildArangoDbId(userId, ArangoDbCollections.Users) },
                { "depth", depth < 2 ? 2 : depth },
                { "offset", offset },
                { "pageSize", pageSize }
            },
            Options = new PostCursorOptions
            {
                FullCount = true
            }
        };

        var response = await db.Cursor.PostCursorAsync<FriendSuggestionRepositoryDto>(postCursorBody: postBody, token: ct);
        
        var totalCount = (int)(response.Extra?.Stats?.FullCount ?? 0);

        return (response.Result.ToList(), totalCount);
    }

    public async Task<(IReadOnlyCollection<FriendProfileRepositoryDto> Items, int TotalCount)> GetMutualFriendsAsync(string userIdA, string userIdB, int page, int pageSize, CancellationToken ct)
    {
        var offset = (page - 1) * pageSize;
        
        var query = $@"
            LET friendsOfA = (
                FOR friend, friendship IN 1..1 ANY @userAId {CollectionName}
                    FILTER friend._id != @userAId
                    FILTER friendship.AcceptedAt != null
                    RETURN friend._id
            )

            FOR mutualFriendCandidate, friendship IN 1..1 ANY @userBId {CollectionName}
                FILTER mutualFriendCandidate._id != @userBId
                FILTER friendship.AcceptedAt != null
                FILTER mutualFriendCandidate._id IN friendsOfA

                SORT mutualFriendCandidate.FullName ASC

                LIMIT @offset, @pageSize

                 RETURN {{
                    Id: mutualFriendCandidate._key,
                    FullName: mutualFriendCandidate.FullName,
                    UserName: mutualFriendCandidate.UserName
                }}
        ";

        var postBody = new PostCursorBody()
        {
            Query = query,
            BindVars = new Dictionary<string, object?>
            {
                { "userAId", ArangoDbUtils.BuildArangoDbId(userIdA, ArangoDbCollections.Users) },
                { "userBId", ArangoDbUtils.BuildArangoDbId(userIdB, ArangoDbCollections.Users) },
                { "offset", offset },
                { "pageSize", pageSize }
            },
            Options = new PostCursorOptions
            {
                FullCount = true
            }
        };
        
        var response = await db.Cursor.PostCursorAsync<FriendProfileRepositoryDto>(postCursorBody: postBody, token: ct);
        
        var totalCount = (int)(response.Extra?.Stats?.FullCount ?? 0);

        return (response.Result.ToList(), totalCount);
    }

    public async Task<FriendshipRequestEntity> InsertFriendshipRequestAsync(string fromUserId, string toUserId,
        CancellationToken ct)
    {
        var query = $@"
            LET fromUser = DOCUMENT(@requesterId)
            LET toUser = DOCUMENT(@receiverId)

            UPSERT {{ _from: @requesterId, _to: @receiverId }}
            INSERT {{ 
                _from: @requesterId, 
                _to: @receiverId, 
                AcceptedAt: null,
                RequestedAt: DATE_ISO8601(DATE_NOW()) 
            }}
            UPDATE {{ }}
            IN {CollectionName}

            RETURN {{
                _key: NEW._key,
                _id: NEW._id,
                _rev: NEW._rev,
                _from: NEW._from,
                _to: NEW._to,
                FromUserName: fromUser.UserName,
                FromName: fromUser.FullName,
                ToUserName: toUser.UserName,
                ToName: toUser.FullName,
                RequestedAt: NEW.RequestedAt,
                AcceptedAt: NEW.AcceptedAt
            }}
        ";

        var bindVars = new Dictionary<string, object>
        {
            { "requesterId", ArangoDbUtils.BuildArangoDbId(fromUserId, ArangoDbCollections.Users)! },
            { "receiverId", ArangoDbUtils.BuildArangoDbId(toUserId, ArangoDbCollections.Users)! }
        };

        var response = await db.Cursor.PostCursorAsync<FriendshipEdgeModel>(query, bindVars, token: ct);

        return response.Result.First().ToEntity();
    }

    public async Task<FriendshipRequestEntity?> UpdateFriendshipRequestAsync(FriendshipRequestEntity entity,
        CancellationToken ct)
    {
        var friendshipEdgeModel = FriendshipEdgeModel.FromEntity(entity);

        var query = $@"
            FOR edge IN {CollectionName}
                FILTER edge._id == @id || edge._key == @id
                
                LET fromUser = DOCUMENT({ArangoDbCollections.Users}, edge._from)
                LET toUser = DOCUMENT({ArangoDbCollections.Users}, edge._to)
                
                UPDATE edge WITH @newModel IN {CollectionName}

                RETURN {{
                    _key: NEW._key,
                    _id: NEW._id,
                    _rev: NEW._rev,
                    _from: NEW._from,
                    _to: NEW._to,
                    FromUserName: fromUser.UserName,
                    FromName: fromUser.FullName,
                    ToUserName: toUser.UserName,
                    ToName: toUser.FullName,
                    RequestedAt: NEW.RequestedAt,
                    AcceptedAt: NEW.AcceptedAt
                }}
        ";

        var bindVars = new Dictionary<string, object>
        {
            { "id", friendshipEdgeModel.Id },
            { "newModel", friendshipEdgeModel }
        };

        var response = await db.Cursor.PostCursorAsync<FriendshipEdgeModel>(query, bindVars, token: ct);

        return response.Result.FirstOrDefault()?.ToEntity();
    }

    public async Task<FriendshipRequestEntity?> FindFriendshipRequestByIdAsync(string requestId, CancellationToken ct)
    {
        var query = $@"
            FOR edge IN {CollectionName}
                FILTER edge._id == @requestId || edge._key == @requestId

                LET fromUser = DOCUMENT({ArangoDbCollections.Users}, edge._from)
                LET toUser = DOCUMENT({ArangoDbCollections.Users}, edge._to)

                RETURN {{
                    _key: edge._key,
                    _id: edge._id,
                    _rev: edge._rev,
                    _from: edge._from,
                    _to: edge._to,
                    FromUserName: fromUser.UserName,
                    FromName: fromUser.FullName,
                    ToUserName: toUser.UserName,
                    ToName: toUser.FullName,
                    RequestedAt: edge.RequestedAt,
                    AcceptedAt: edge.AcceptedAt
                }}
        ";

        var bindVars = new Dictionary<string, object>
        {
            { "requestId", requestId }
        };

        var response = await db.Cursor.PostCursorAsync<FriendshipEdgeModel>(query, bindVars, token: ct);

        return response.Result.FirstOrDefault()?.ToEntity();
    }

    public async Task<FriendshipRequestEntity?> DeleteFriendshipRequestByIdAsync(string requestId, CancellationToken ct)
    {
        var query = $@"
            FOR edge IN {CollectionName}
                FILTER edge._id == @requestId || edge._key == @requestId

                LET fromUser = DOCUMENT(edge._from)
                LET toUser = DOCUMENT(edge._to)
                
                REMOVE edge IN {CollectionName}

                RETURN {{
                    _key: edge._key,
                    _id: edge._id,
                    _rev: edge._rev,
                    _from: edge._from,
                    _to: edge._to,
                    FromUserName: fromUser.UserName,
                    FromName: fromUser.FullName,
                    ToUserName: toUser.UserName,
                    ToName: toUser.FullName,
                    RequestedAt: edge.RequestedAt,
                    AcceptedAt: edge.AcceptedAt
                }}
        ";

        var bindVars = new Dictionary<string, object>
        {
            { "requestId", requestId }
        };

        var response = await db.Cursor.PostCursorAsync<FriendshipEdgeModel>(query, bindVars, token: ct);

        return response.Result.FirstOrDefault()?.ToEntity();
    }
}