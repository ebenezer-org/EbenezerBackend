using EbenezerBackend.Shared.Web.Dtos.Pagination;

namespace EbenezerBackend.Features.Prayers.Presentation.Dtos.Search;

public record SearchPrayersRequestDto(
    PaginationRequestDto Pagination,
    string? AuthorUserName = null,
    string? CategoryId = null,
    string? Text = null
);

