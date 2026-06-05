using System;

namespace EbenezerBackend.Features.Categories.Presentation.Dtos.Create;

public record CreateCategoryResponseDto(
    string Id,
    string OwnerUsername,
    string Name,
    string Description,
    string ColorHex,
    bool IsPublic,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
