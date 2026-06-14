using System.Collections.Generic;
using EbenezerBackend.Features.Friendships.Presentation.Dtos.Shared;
using EbenezerBackend.Shared.Web.Dtos.Pagination;

namespace EbenezerBackend.Features.Friendships.Presentation.Dtos.GetFriendSuggestions;

public record GetFriendSuggestionsResponseDto(
    IReadOnlyCollection<FriendSuggestionDto> Items,
    int Page,
    int PageSize,
    int TotalCount
    ) : PaginatedResponseDto<FriendSuggestionDto>(Items,  Page, PageSize, TotalCount);