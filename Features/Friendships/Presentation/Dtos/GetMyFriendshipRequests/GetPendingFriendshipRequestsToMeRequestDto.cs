using EbenezerBackend.Shared.Web.Dtos.Pagination;

namespace EbenezerBackend.Features.Friendships.Presentation.Dtos.GetMyFriendshipRequests;

public record GetPendingFriendshipRequestsToMeRequestDto(
    int Page = 1, 
    int PageSize = 10
) : PaginationRequestDto(Page, PageSize);