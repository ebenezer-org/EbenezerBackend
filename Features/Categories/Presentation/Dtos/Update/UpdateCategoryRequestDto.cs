namespace EbenezerBackend.Features.Categories.Presentation.Dtos.Update;

public record UpdateCategoryRequestDto(
    string Name,
    string Description,
    string ColorHex,
    bool IsPublic
);
