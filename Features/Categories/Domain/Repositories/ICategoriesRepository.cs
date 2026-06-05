using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EbenezerBackend.Features.Categories.Domain.Entities;

namespace EbenezerBackend.Features.Categories.Domain.Repositories;

public interface ICategoriesRepository
{
    Task<CategoryEntity> CreateCategoryAsync(CategoryEntity category, CancellationToken ct);
    Task<CategoryEntity?> FindCategoryByIdAsync(string categoryId, CancellationToken ct);
    Task<IReadOnlyCollection<CategoryEntity>> FindCategoriesByUsernameAsync(string username, CancellationToken ct);
    Task<CategoryEntity?> UpdateCategoryAsync(string categoryId, CategoryEntity category, CancellationToken ct);
    Task<bool> DeleteCategoryAsync(string categoryId, string ownerUsername, CancellationToken ct);
}
