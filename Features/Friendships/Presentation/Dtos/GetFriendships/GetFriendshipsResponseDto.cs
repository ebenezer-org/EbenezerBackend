using EbenezerBackend.Features.Friendships.Presentation.Dtos.Shared;
using EbenezerBackend.Shared.Web.Dtos.Pagination;

namespace EbenezerBackend.Features.Friendships.Presentation.Dtos.GetFriendships;

public record GetFriendshipsResponseDto(
    IReadOnlyCollection<FriendDto> Items,
    int Page,
    int PageSize,
    int TotalCount
    ) : PaginatedResponseDto<FriendDto>(Items, Page, PageSize, TotalCount);