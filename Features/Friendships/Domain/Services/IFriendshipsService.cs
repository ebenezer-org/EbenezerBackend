using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EbenezerBackend.Features.Friendships.Presentation.Dtos.GetFriendships;
using EbenezerBackend.Features.Friendships.Presentation.Dtos.GetFriendSuggestions;
using EbenezerBackend.Features.Friendships.Presentation.Dtos.GetMyFriendshipRequests;
using EbenezerBackend.Features.Friendships.Presentation.Dtos.GetUserMutualFriends;
using EbenezerBackend.Features.Friendships.Presentation.Dtos.SendFriendRequest;
using EbenezerBackend.Features.Friendships.Presentation.Dtos.Shared;
using EbenezerBackend.Shared.Web.Dtos.Pagination;

namespace EbenezerBackend.Features.Friendships.Domain.Services;

public interface IFriendshipsService
{
    Task<FriendshipRequestDto> SendFriendRequestAsync(SendFriendRequestRequestDto request, CancellationToken ct);
    Task<GetPendingFriendshipRequestsToMeResponseDto> GetPendingFriendshipRequestsAsync(GetPendingFriendshipRequestsToMeRequestDto request, CancellationToken ct);
    Task<GetFriendshipsResponseDto> GetFriendships(GetFriendshipsRequestDto request, CancellationToken ct);
    Task<FriendshipRequestDto> AcceptFriendRequestAsync(string requestId, CancellationToken ct);

    Task<GetFriendSuggestionsResponseDto> GetFriendSuggestionsAsync(GetFriendSuggestionsRequestDto request,
        CancellationToken ct);
    Task<GetMyMutualFriendsResponseDto> GetUsersMutualFriendsWithAsync(string otherUserId, PaginationRequestDto pagination,
        CancellationToken ct);
    Task DeclineFriendRequestAsync(string requestId, CancellationToken ct);
    Task CancelFriendRequestAsync(string requestId, CancellationToken ct);
    Task RemoveFriendAsync(string friendId, CancellationToken ct);
}