namespace EbenezerBackend.Features.Categories.Presentation.Dtos.Get;

public record GetCategoryResponseDto(
    string Id,
    string OwnerUsername,
    string Name,
    string Description,
    string ColorHex,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
