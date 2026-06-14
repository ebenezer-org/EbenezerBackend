using System.Collections.Generic;
using EbenezerBackend.Features.Friendships.Presentation.Dtos.Shared;
using EbenezerBackend.Shared.Web.Dtos.Pagination;

namespace EbenezerBackend.Features.Friendships.Presentation.Dtos.GetFriendSuggestions;

public record GetFriendSuggestionsRequestDto(
    int Page = 1,
    int PageSize = 10
    ) : PaginationRequestDto(Page, PageSize);