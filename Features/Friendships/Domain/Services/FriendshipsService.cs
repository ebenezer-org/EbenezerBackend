using EbenezerBackend.Features.Friendships.Domain.Entities;
using EbenezerBackend.Features.Friendships.Domain.Enums;
using EbenezerBackend.Features.Friendships.Domain.Exceptions;
using EbenezerBackend.Features.Friendships.Domain.Repositories;
using EbenezerBackend.Features.Friendships.Presentation.Dtos.GetFriendships;
using EbenezerBackend.Features.Friendships.Presentation.Dtos.GetFriendSuggestions;
using EbenezerBackend.Features.Friendships.Presentation.Dtos.GetMyFriendshipRequests;
using EbenezerBackend.Features.Friendships.Presentation.Dtos.GetUserMutualFriends;
using EbenezerBackend.Features.Friendships.Presentation.Dtos.SendFriendRequest;
using EbenezerBackend.Features.Friendships.Presentation.Dtos.Shared;
using EbenezerBackend.Shared.Web.Dtos;
using EbenezerBackend.Shared.Web.Dtos.Pagination;
using EbenezerBackend.Shared.Web.Services.EnsureUserExists;
using EbenezerBackend.Shared.Web.Services.UserContext;

namespace EbenezerBackend.Features.Friendships.Domain.Services;

public class FriendshipsService(
    IFriendshipsRepository repository,
    IEnsureUserExistsService ensureUserExistsService,
    IUserContext userContext
    ) : IFriendshipsService
{
    public async Task<FriendshipRequestDto> SendFriendRequestAsync(SendFriendRequestRequestDto request, CancellationToken ct)
    {
        var fromUserId = userContext.Id;
        var toUserId = request.ToUserId;
        
        if (fromUserId == toUserId) 
        {
            throw new CannotBefriendYourselfException();
        }
        
        await ensureUserExistsService.EnsureExistsByIdAsync(toUserId, ct);

        var (friendshipRequestList, _) =
            await repository.SearchFriendships(
                containsUserIds: (fromUserId, toUserId),
                page: 1,
                pageSize: 1,
                ct: ct
                );
        
        if (friendshipRequestList.Count > 0) 
        {
            var existingRequest = friendshipRequestList.First();
            
            if (existingRequest.IsAccepted)
            {
                throw new FriendshipAlreadyExistsException(existingRequest.TargetName);
            }

            if (existingRequest.IsTarget(userContext.Id))
            {
                return await AcceptFriendRequestAsync(existingRequest.Id, ct);
            }
            
            return new FriendshipRequestDto(
                Id: existingRequest.Id,
                RequesterId: existingRequest.RequesterId,
                RequesterName: existingRequest.RequesterName,
                TargetId: existingRequest.TargetId,
                TargetName: existingRequest.TargetName,
                RequestedAt: existingRequest.RequestedAt,
                AcceptedAt: existingRequest.AcceptedAt
            );
        }
        
        var friendshipRequestEntity = await repository.InsertFriendshipRequestAsync(fromUserId, toUserId, ct);
        
        return new FriendshipRequestDto(
            Id: friendshipRequestEntity.Id,
            RequesterId: friendshipRequestEntity.RequesterId,
            RequesterName: friendshipRequestEntity.RequesterName,
            TargetId: friendshipRequestEntity.TargetId,
            TargetName: friendshipRequestEntity.TargetName,
            RequestedAt: friendshipRequestEntity.RequestedAt,
            AcceptedAt: friendshipRequestEntity.AcceptedAt
        );
    }

    public async Task<GetPendingFriendshipRequestsToMeResponseDto> GetPendingFriendshipRequestsAsync(GetPendingFriendshipRequestsToMeRequestDto request, CancellationToken ct)
    {
        var userId = userContext.Id;
        
        var (entityList, totalCount) =
            await repository.SearchFriendships(
                toUserId: userId,
                status: FriendshipStatusEnum.Pending,
                page: request.Page,
                pageSize: request.PageSize,
                ct: ct
                );
        
        var dtoList = entityList.Select(entity => new FriendshipRequestDto(
            Id: entity.Id,
           RequesterId: entity.RequesterId,
           RequesterName: entity.RequesterName,
           TargetId: entity.TargetId,
           TargetName: entity.TargetName,
           RequestedAt: entity.RequestedAt,
           AcceptedAt: entity.AcceptedAt
           )).ToList();

        return new GetPendingFriendshipRequestsToMeResponseDto(
            Items: dtoList,
            Page: request.Page,
            PageSize: request.PageSize,
            TotalCount: totalCount
            );
    }

    public async Task<GetFriendshipsResponseDto> GetFriendships(GetFriendshipsRequestDto request, CancellationToken ct)
    {
        if (request.OfUserId != null)
            await ensureUserExistsService.EnsureExistsByIdAsync(request.OfUserId, ct);
            
        
        var userId = request.OfUserId ?? userContext.Id;
        
        var (friendshipEntityList, totalCount) =
            await repository.SearchFriendships(
                containsUserIds: (userId, null),
                status: FriendshipStatusEnum.Accepted,
                page: request.Page,
                pageSize: request.PageSize,
                ct: ct
                );
        
        var friendsDtoList = friendshipEntityList.Select(entity => new FriendDto(
            Id: entity.Id,
            Friend: new UserEssentialDto(
                Id: entity.GetFriendId(userId),
                UserName: entity.GetFriendUserName(userId),
                FullName: entity.GetFriendName(userId)
                ),
            RequestedAt: entity.RequestedAt,
            AcceptedAt: (DateTime)entity.AcceptedAt!
        )).ToList();
        
        return new GetFriendshipsResponseDto(
            Items: friendsDtoList,
            Page: request.Page,
            PageSize: request.PageSize,
            TotalCount: totalCount
            );
    }

    public async Task<FriendshipRequestDto> AcceptFriendRequestAsync(string requestId, CancellationToken ct)
    {
        var friendshipRequestEntity = await GetUserFriendshipRequestById(requestId, ct);
        
        if (friendshipRequestEntity == null)
        {
            throw new FriendshipRequestNotFoundException();
        }
        
        friendshipRequestEntity.AcceptRequest(userContext.Id);
        
        var result = await repository.UpdateFriendshipRequestAsync(friendshipRequestEntity, ct);

        if (result == null)
        {
            throw new FriendshipRequestNotFoundException();
        }

        return new FriendshipRequestDto(
            Id: result.Id,
            RequesterId: result.RequesterId, 
            RequesterName: result.RequesterName, 
            TargetId: result.TargetId, 
            TargetName: result.TargetName,
            RequestedAt: result.RequestedAt, 
            AcceptedAt: result.AcceptedAt
            );
    }

    public async Task<GetFriendSuggestionsResponseDto> GetFriendSuggestionsAsync(GetFriendSuggestionsRequestDto request, CancellationToken ct)
    {
        var userId = userContext.Id;
        
        var (suggestionsList, totalCount) = await repository.GetFriendSuggestionsAsync(userId: userId, depth: 2, page: request.Page, pageSize: request.PageSize , ct: ct);
        
        var suggestionsListDto = 
            suggestionsList.Select(
                item => new FriendSuggestionDto(
                    Id: item.FriendSuggestion.Id,
                    UserName: item.FriendSuggestion.UserName,
                    Name: item.FriendSuggestion.FullName,
                    OldestMutualFriend: new UserEssentialDto(
                        Id: item.OldestMutualFriend.Id,
                        UserName: item.OldestMutualFriend.UserName,
                        FullName: item.OldestMutualFriend.FullName
                        ),
                    MutualFriendsCount: item.MutualFriendsCount)
                );
        
        return new GetFriendSuggestionsResponseDto(
            Items: suggestionsListDto.ToList(),
            Page: request.Page,
            PageSize: Math.Min(request.PageSize, totalCount),
            TotalCount: totalCount
            );
    }

    public async Task<GetMyMutualFriendsResponseDto> GetUsersMutualFriendsWithAsync(string otherUserId,
        PaginationRequestDto pagination, CancellationToken ct)
    {
        await ensureUserExistsService.EnsureExistsByIdAsync(otherUserId, ct);
        
        var userId = userContext.Id;
        
        var (friendsListResult, totalCount) = await repository.GetMutualFriendsAsync(userIdA: userId, userIdB: otherUserId, page: pagination.Page, pageSize: pagination.PageSize, ct: ct);
        
        var friendListDto = friendsListResult.Select(item => new UserEssentialDto(
            Id: item.Id,
            UserName: item.UserName,
            FullName: item.FullName
            )
        ).ToList();

        return new GetMyMutualFriendsResponseDto(
            Items: friendListDto,
            Page: pagination.Page,
            PageSize: Math.Min(friendListDto.Count,  pagination.PageSize),
            TotalCount:  totalCount
            );
    }

    public async Task DeclineFriendRequestAsync(string requestId, CancellationToken ct)
    {
        var friendshipRequestEntity = await GetUserFriendshipRequestById(requestId, ct);
        
        if (!friendshipRequestEntity.IsTarget(userContext.Id)) 
        {
            throw new ForbiddenToReadOrUpdateFriendshipRequest();
        }
        
        await repository.DeleteFriendshipRequestByIdAsync(requestId, ct);
    }
    
    public async Task CancelFriendRequestAsync(string requestId, CancellationToken ct)
    {
        var friendshipRequestEntity = await GetUserFriendshipRequestById(requestId, ct);
        
        if (!friendshipRequestEntity.IsRequester(userContext.Id)) 
        {
            throw new ForbiddenToReadOrUpdateFriendshipRequest();
        }
        
        await repository.DeleteFriendshipRequestByIdAsync(requestId, ct);

    }

    public async Task RemoveFriendAsync(string friendId, CancellationToken ct)
    {
        await ensureUserExistsService.EnsureExistsByIdAsync(friendId, ct);

        
        var (friendshipRequestEntities, totalCount) =
            await repository.SearchFriendships(
                containsUserIds: (userContext.Id, friendId),
                page: 1,
                pageSize: 1,
                ct: ct
                );
        
        if (totalCount == 0) 
        {
            throw new CannotRemoveFriendshipNonExistentException();
        }
        
        var entityToBeDeleted = friendshipRequestEntities.First();
        
        await repository.DeleteFriendshipRequestByIdAsync(entityToBeDeleted.Id, ct);
    }
    
    private async Task<FriendshipRequestEntity> GetUserFriendshipRequestById(string requestId, CancellationToken ct)
    {
        var result = await repository.FindFriendshipRequestByIdAsync(requestId, ct);
        
        if (result == null) 
        {
            throw new FriendshipRequestNotFoundException();
        }
        
        if (!result.ContainsUser(userContext.Id)) 
        {
            throw new ForbiddenToReadOrUpdateFriendshipRequest();
        }

        return result!;
    }
}