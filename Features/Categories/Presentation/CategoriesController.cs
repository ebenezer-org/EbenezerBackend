using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EbenezerBackend.Features.Categories.Domain.Services;
using EbenezerBackend.Features.Categories.Presentation.Dtos.Create;
using EbenezerBackend.Features.Categories.Presentation.Dtos.Get;
using EbenezerBackend.Features.Categories.Presentation.Dtos.Update;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EbenezerBackend.Features.Categories.Presentation;

[ApiController]
[Route("[controller]")]
public class CategoriesController(ICategoriesService categoriesService) : ControllerBase
{
    [Authorize]
    [HttpPost("new")]
    public async Task<ActionResult<CreateCategoryResponseDto>> CreateCategory(
        [FromBody] CreateCategoryRequestDto request,
        CancellationToken ct)
    {
        var response = await categoriesService.CreateCategoryAsync(request, ct);

        return CreatedAtAction(nameof(GetCategoryById), new { categoryId = response.Id }, response);
    }

    [HttpGet("{categoryId}")]
    public async Task<ActionResult<GetCategoryResponseDto>> GetCategoryById(
        [FromRoute] string categoryId,
        CancellationToken ct)
    {
        var response = await categoriesService.GetCategoryByIdAsync(categoryId, ct);

        return Ok(response);
    }

    [HttpGet("user/{username}")]
    public async Task<ActionResult<IReadOnlyCollection<GetCategoryResponseDto>>> GetCategoriesByUsername(
        [FromRoute] string username,
        CancellationToken ct)
    {
        var response = await categoriesService.GetCategoriesByUsernameAsync(username, ct);

        return Ok(response);
    }

    [Authorize]
    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyCollection<GetCategoryResponseDto>>> GetMyCategories(CancellationToken ct)
    {
        var response = await categoriesService.GetMyCategoriesAsync(ct);

        return Ok(response);
    }

    [Authorize]
    [HttpPut("{categoryId}")]
    public async Task<ActionResult<UpdateCategoryResponseDto>> UpdateCategory(
        [FromRoute] string categoryId,
        [FromBody] UpdateCategoryRequestDto request,
        CancellationToken ct)
    {
        var response = await categoriesService.UpdateCategoryAsync(categoryId, request, ct);

        return Ok(response);
    }

    [Authorize]
    [HttpDelete("{categoryId}")]
    public async Task<IActionResult> DeleteCategory([FromRoute] string categoryId, CancellationToken ct)
    {
        await categoriesService.DeleteCategoryAsync(categoryId, ct);

        return NoContent();
    }
}
