using EbenezerBackend.Features.Categories.Domain.Entities;
using EbenezerBackend.Features.Categories.Domain.Repositories;
using EbenezerBackend.Features.Categories.Presentation.Dtos.Create;
using EbenezerBackend.Features.Categories.Presentation.Dtos.Get;
using EbenezerBackend.Features.Categories.Presentation.Dtos.Update;
using EbenezerBackend.Shared.Exceptions;
using EbenezerBackend.Shared.Services.UserContext;

namespace EbenezerBackend.Features.Categories.Domain.Services;

public class CategoriesService(ICategoriesRepository repository, IUserContext userContext) : ICategoriesService
{
    public async Task<CreateCategoryResponseDto> CreateCategoryAsync(CreateCategoryRequestDto request, CancellationToken ct)
    {
        var category = new CategoryEntity(userContext.UserName, request.Name, request.Description, request.ColorHex);
        var createdCategory = await repository.CreateCategoryAsync(category, ct);

        return ToCreateResponse(createdCategory);
    }

    public async Task<GetCategoryResponseDto> GetCategoryByIdAsync(string categoryId, CancellationToken ct)
    {
        var category = await repository.FindCategoryByIdAsync(categoryId, ct);

        return category is null
            ? throw new NotFoundException("Categoria não encontrada")
            : ToGetResponse(category);
    }

    public async Task<IReadOnlyCollection<GetCategoryResponseDto>> GetCategoriesByUsernameAsync(string username, CancellationToken ct)
    {
        var categories = await repository.FindCategoriesByUsernameAsync(username, ct);

        return categories.Select(ToGetResponse).ToList();
    }

    public async Task<IReadOnlyCollection<GetCategoryResponseDto>> GetMyCategoriesAsync(CancellationToken ct)
    {
        var categories = await repository.FindCategoriesByUsernameAsync(userContext.UserName, ct);

        return categories.Select(ToGetResponse).ToList();
    }

    public async Task<UpdateCategoryResponseDto> UpdateCategoryAsync(string categoryId, UpdateCategoryRequestDto request, CancellationToken ct)
    {
        var category = new CategoryEntity(userContext.UserName, request.Name, request.Description, request.ColorHex);
        var updatedCategory = await repository.UpdateCategoryAsync(categoryId, category, ct);

        return updatedCategory is null
            ? throw new NotFoundException("Categoria não encontrada")
            : ToUpdateResponse(updatedCategory);
    }

    public async Task DeleteCategoryAsync(string categoryId, CancellationToken ct)
    {
        var deleted = await repository.DeleteCategoryAsync(categoryId, userContext.UserName, ct);

        if (!deleted)
        {
            throw new NotFoundException("Categoria não encontrada");
        }
    }

    private static CreateCategoryResponseDto ToCreateResponse(CategoryEntity category)
    {
        return new CreateCategoryResponseDto(
            category.Id ?? string.Empty,
            category.OwnerUsername,
            category.Name,
            category.Description,
            category.ColorHex,
            category.CreatedAt,
            category.UpdatedAt);
    }

    private static GetCategoryResponseDto ToGetResponse(CategoryEntity category)
    {
        return new GetCategoryResponseDto(
            category.Id ?? string.Empty,
            category.OwnerUsername,
            category.Name,
            category.Description,
            category.ColorHex,
            category.CreatedAt,
            category.UpdatedAt);
    }

    private static UpdateCategoryResponseDto ToUpdateResponse(CategoryEntity category)
    {
        return new UpdateCategoryResponseDto(
            category.Id ?? string.Empty,
            category.OwnerUsername,
            category.Name,
            category.Description,
            category.ColorHex,
            category.CreatedAt,
            category.UpdatedAt);
    }
}
