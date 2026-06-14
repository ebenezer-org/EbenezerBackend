using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EbenezerBackend.Features.Categories.Domain.Entities;
using EbenezerBackend.Features.Categories.Domain.Repositories;
using EbenezerBackend.Features.Categories.Presentation.Dtos.Create;
using EbenezerBackend.Features.Categories.Presentation.Dtos.Get;
using EbenezerBackend.Features.Categories.Presentation.Dtos.Update;
using EbenezerBackend.Shared.Web.Exceptions;
using EbenezerBackend.Shared.Web.Services.UserContext;

namespace EbenezerBackend.Features.Categories.Domain.Services;

public class CategoriesService(ICategoriesRepository repository, IUserContext userContext) : ICategoriesService
{
    public async Task<CreateCategoryResponseDto> CreateCategoryAsync(CreateCategoryRequestDto request, CancellationToken ct)
    {
        var category = new CategoryEntity(
            userContext.UserName,
            request.Name,
            request.Description,
            request.ColorHex,
            request.IsPublic);
        var createdCategory = await repository.CreateCategoryAsync(category, ct);

        return ToCreateResponse(createdCategory);
    }

    public async Task<GetCategoryResponseDto> GetCategoryByIdAsync(string categoryId, CancellationToken ct)
    {
        var category = await repository.FindCategoryByIdAsync(categoryId, ct);

        if (category is null)
        {
            throw new NotFoundException("Categoria não encontrada");
        }

        if (category.IsPublic)
        {
            return ToGetResponse(category);
        }

        if (!userContext.IsAuthenticated || userContext.UserName != category.OwnerUsername)
        {
            throw new NotFoundException("Categoria não encontrada");
        }

        return ToGetResponse(category);
    }

    public async Task<IReadOnlyCollection<GetCategoryResponseDto>> GetCategoriesByUsernameAsync(string username, CancellationToken ct)
    {
        var includePrivate = userContext.IsAuthenticated && userContext.UserName == username;
        var categories = await repository.FindCategoriesByUsernameAsync(username, includePrivate, ct);

        return categories.Select(ToGetResponse).ToList();
    }

    public async Task<IReadOnlyCollection<GetCategoryResponseDto>> GetMyCategoriesAsync(CancellationToken ct)
    {
        var categories = await repository.FindCategoriesByUsernameAsync(userContext.UserName, true, ct);

        return categories.Select(ToGetResponse).ToList();
    }

    public async Task<UpdateCategoryResponseDto> UpdateCategoryAsync(string categoryId, UpdateCategoryRequestDto request, CancellationToken ct)
    {
        var category = new CategoryEntity(
            userContext.UserName,
            request.Name,
            request.Description,
            request.ColorHex,
            request.IsPublic);
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
            category.IsPublic,
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
            category.IsPublic,
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
            category.IsPublic,
            category.CreatedAt,
            category.UpdatedAt);
    }
}
