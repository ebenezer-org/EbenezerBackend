using EbenezerBackend.Shared.Web.Dtos.Pagination;

namespace EbenezerBackend.Features.Friendships.Presentation.Dtos.GetFriendships;

public record GetFriendshipsRequestDto(
    int Page = 1,
    int PageSize = 10,
    string? OfUserId = null
    ) : PaginationRequestDto(Page, PageSize);