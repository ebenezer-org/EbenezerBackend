using System.Collections.Generic;
using EbenezerBackend.Features.Friendships.Presentation.Dtos.Shared;
using EbenezerBackend.Shared.Web.Dtos.Pagination;

namespace EbenezerBackend.Features.Friendships.Presentation.Dtos.GetMyFriendshipRequests;

public record GetPendingFriendshipRequestsToMeResponseDto(
    IReadOnlyCollection<FriendshipRequestDto> Items,
    int Page,
    int PageSize,
    int TotalCount
) : PaginatedResponseDto<FriendshipRequestDto>(Items, Page, PageSize, TotalCount);