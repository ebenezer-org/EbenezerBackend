using System.Collections.Generic;
using EbenezerBackend.Shared.Web.Dtos;
using EbenezerBackend.Shared.Web.Dtos.Pagination;

namespace EbenezerBackend.Features.Friendships.Presentation.Dtos.GetUserMutualFriends;

public record GetMyMutualFriendsResponseDto(
    IReadOnlyCollection<UserEssentialDto> Items,
    int Page,
    int PageSize,
    int TotalCount
    ) : PaginatedResponseDto<UserEssentialDto>(Items, Page, PageSize, TotalCount);