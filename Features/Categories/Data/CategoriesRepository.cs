using EbenezerBackend.Features.Categories.Data.Models;
using EbenezerBackend.Features.Categories.Domain.Entities;
using EbenezerBackend.Features.Categories.Domain.Repositories;
using EbenezerBackend.Shared.Data;

namespace EbenezerBackend.Features.Categories.Data;

public class CategoriesRepository : BaseRepository<CategoryModel>, ICategoriesRepository
{
    public Task<CategoryEntity> CreateCategoryAsync(CategoryEntity category, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<CategoryEntity?> FindCategoryByIdAsync(string categoryId, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<IReadOnlyCollection<CategoryEntity>> FindCategoriesByUsernameAsync(string username, bool includePrivate, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<CategoryEntity?> UpdateCategoryAsync(string categoryId, CategoryEntity category, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<bool> DeleteCategoryAsync(string categoryId, string ownerUsername, CancellationToken ct)
        => throw new NotImplementedException();
}
