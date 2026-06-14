namespace EbenezerBackend.Shared.Web.Dtos.Pagination;

public record PaginationRequestDto(
    int Page,
    int PageSize
)
{
    public int Page { get; } = Page > 0 ? Page : 1;
    public int PageSize { get; } = PageSize > 0 ? PageSize : 10;
};