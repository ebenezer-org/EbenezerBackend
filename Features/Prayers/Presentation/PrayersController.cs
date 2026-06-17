using EbenezerBackend.Features.Prayers.Domain.Services;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.AddComment;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.Create;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.Get;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.List;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.Search;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.Timeline;
using EbenezerBackend.Features.Prayers.Presentation.Dtos.Update;
using EbenezerBackend.Shared.Web.Dtos.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EbenezerBackend.Features.Prayers.Presentation;

[ApiController]
[Route("[controller]")]
public class PrayersController(IPrayersService prayersService) : ControllerBase
{
    [HttpGet("{prayerId}")]
    public async Task<ActionResult<GetPrayerResponseDto>> GetPrayerById([FromRoute] string prayerId, CancellationToken ct)
    {
        var result = await prayersService.GetPrayerByIdAsync(prayerId, ct);

        return Ok(result);
    }

    [Authorize]
    [HttpPost("new")]
    public async Task<ActionResult<CreatePrayerResponseDto>> CreatePost(
        [FromBody] CreatePrayerRequestDto request,
        CancellationToken ct)
    {
        var result = await prayersService.CreatePostAsync(request, ct);

        return Ok(result);
    }

    [Authorize]
    [HttpPut("{prayerId}")]
    public async Task<ActionResult<UpdatePrayerResponseDto>> UpdatePrayer(
        [FromRoute] string prayerId,
        [FromBody] UpdatePrayerRequestDto request,
        CancellationToken ct)
    {
        var result = await prayersService.UpdatePrayerAsync(prayerId, request, ct);

        return Ok(result);
    }

    [Authorize]
    [HttpDelete("{prayerId}")]
    public async Task<IActionResult> DeletePrayer([FromRoute] string prayerId, CancellationToken ct)
    {
        await prayersService.DeletePrayerAsync(prayerId, ct);

        return NoContent();
    }

    [Authorize]
    [HttpPost("{prayerId}/support")]
    public async Task<IActionResult> AddSupportReaction(
        [FromRoute] string prayerId,
        CancellationToken ct)
    {
        await prayersService.AddSupportReactionAsync(prayerId, ct);

        return NoContent();
    }
    
    [Authorize]
    [HttpPost("{prayerId}/support-remove")]
    public async Task<IActionResult> RemoveSupportReaction(
        [FromRoute] string prayerId,
        CancellationToken ct)
    {
        await prayersService.RemoveSupportReactionAsync(prayerId, ct);

        return NoContent();
    }
    
    [Authorize]
    [HttpPost("{prayerId}/comment")]
    public async Task<ActionResult<AddCommentResponseDto>> AddComment(
        [FromRoute] string prayerId,
        [FromBody] AddCommentRequestDto request,
        CancellationToken ct)
    {
        var result = await prayersService.AddCommentAsync(prayerId, request, ct);

        return Ok(result);
    }

    [Authorize]
    [HttpGet("timeline")]
    public async Task<ActionResult<PaginatedResponseDto<TimelinePrayerResponseDto>>> GetTimeline(
        [FromQuery] PaginationRequestDto pagination,
        CancellationToken ct = default)
    {
        var result = await prayersService.GetTimelineAsync(pagination.Page, pagination.PageSize, ct);

        return Ok(result);
    }

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyCollection<ListPrayerResponseDto>>> SearchPrayers(
        [FromQuery] SearchPrayersRequestDto request,
        CancellationToken ct)
    {
        var result = await prayersService.SearchPrayersAsync(request, ct);

        return Ok(result);
    }
}
