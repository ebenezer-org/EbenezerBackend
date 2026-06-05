namespace EbenezerBackend.Features.Prayers.Presentation.Dtos.Search;

public record SearchPrayersRequestDto(
    string? AuthorUserName = null,
    string? CategoryId = null,
    string? Text = null,
    int Page = 1,
    int PageSize = 20
);

