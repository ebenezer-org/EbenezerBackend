using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EbenezerBackend.Features.Categories.Presentation.Dtos.Create;
using EbenezerBackend.Features.Categories.Presentation.Dtos.Get;
using EbenezerBackend.Features.Categories.Presentation.Dtos.Update;

namespace EbenezerBackend.Features.Categories.Domain.Services;

public interface ICategoriesService
{
    Task<CreateCategoryResponseDto> CreateCategoryAsync(CreateCategoryRequestDto request, CancellationToken ct);
    Task<GetCategoryResponseDto> GetCategoryByIdAsync(string categoryId, CancellationToken ct);
    Task<IReadOnlyCollection<GetCategoryResponseDto>> GetCategoriesByUsernameAsync(string username, CancellationToken ct);
    Task<IReadOnlyCollection<GetCategoryResponseDto>> GetMyCategoriesAsync(CancellationToken ct);
    Task<UpdateCategoryResponseDto> UpdateCategoryAsync(string categoryId, UpdateCategoryRequestDto request, CancellationToken ct);
    Task DeleteCategoryAsync(string categoryId, CancellationToken ct);
}
