using System;

namespace EbenezerBackend.Features.Categories.Presentation.Dtos.Get;

public record GetCategoryResponseDto(
    string Id,
    string OwnerUsername,
    string Name,
    string Description,
    string ColorHex,
    bool IsPublic,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
