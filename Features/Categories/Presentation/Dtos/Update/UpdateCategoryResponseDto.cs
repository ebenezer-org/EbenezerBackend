namespace EbenezerBackend.Features.Categories.Presentation.Dtos.Update;

public record UpdateCategoryResponseDto(
    string Id,
    string OwnerUsername,
    string Name,
    string Description,
    string ColorHex,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
