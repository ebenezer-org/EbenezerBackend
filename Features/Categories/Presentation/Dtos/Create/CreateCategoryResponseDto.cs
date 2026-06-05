namespace EbenezerBackend.Features.Categories.Presentation.Dtos.Create;

public record CreateCategoryResponseDto(
    string Id,
    string OwnerUsername,
    string Name,
    string Description,
    string ColorHex,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
