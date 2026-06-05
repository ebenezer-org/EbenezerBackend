namespace EbenezerBackend.Features.Categories.Presentation.Dtos.Create;

public record CreateCategoryRequestDto(
    string Name,
    string Description,
    string ColorHex,
    bool IsPublic = true
);
