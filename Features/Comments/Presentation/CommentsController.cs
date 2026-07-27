using EbenezerBackend.Features.Comments.Domain.Enums;
using EbenezerBackend.Features.Comments.Domain.Services;
using EbenezerBackend.Features.Comments.Presentation.Dtos.PostComment;
using EbenezerBackend.Features.Comments.Presentation.Dtos.Shared;
using EbenezerBackend.Features.Comments.Presentation.Dtos.UpdateComment;
using EbenezerBackend.Shared.Web.Dtos.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EbenezerBackend.Features.Comments.Presentation;

[ApiController]
[Route("[controller]")]
public class CommentsController(ICommentsService commentsService) : ControllerBase
{
    [Authorize]
    [HttpGet("prayer/{prayerId}")]
    public async Task<ActionResult<PaginatedResponseDto<CommentDto>>> GetCommentsOfPrayer(
        [FromRoute] string prayerId,
        [FromQuery] PaginationRequestDto pagination,
        CancellationToken ct)
    {
        var result = await commentsService.GetCommentsAsync(prayerId, CommentParentTypeEnum.Prayer, pagination, ct);

        return Ok(result);
    }
    
    [Authorize]
    [HttpGet("comment/{commentId}")]
    public async Task<ActionResult<PaginatedResponseDto<CommentDto>>> GetCommentsOfComment(
        [FromRoute] string commentId,
        [FromQuery] PaginationRequestDto pagination,
        CancellationToken ct)
    {
        var result = await commentsService.GetCommentsAsync(commentId, CommentParentTypeEnum.Comment, pagination, ct);

        return Ok(result);
    }

    [Authorize]
    [HttpPost("prayer/{parentId}")]
    public async Task<ActionResult<PostCommentResponseDto>> PostCommentOnPrayer(
        [FromRoute] string parentId,
        [FromBody] PostCommentRequestDto body,
        CancellationToken ct)
    {
        var result = await commentsService.PostCommentAsync(parentId, CommentParentTypeEnum.Prayer, body, ct);

        return Ok(result);
    }
    
    [Authorize]
        [HttpPost("comment/{parentId}")]
        public async Task<ActionResult<PostCommentResponseDto>> PostCommentOnComment(
            [FromRoute] string parentId,
            [FromBody] PostCommentRequestDto body,
            CancellationToken ct)
        {
            var result = await commentsService.PostCommentAsync(parentId, CommentParentTypeEnum.Comment, body, ct);
    
            return Ok(result);
        }

    [Authorize]
    [HttpPut("{commentId}")]
    public async Task<ActionResult<UpdateCommentResponseDto>> UpdateComment(
        [FromRoute] string commentId,
        [FromBody] UpdateCommentRequestDto body,
        CancellationToken ct)
    {
        var result = await commentsService.UpdateCommentAsync(commentId, body, ct);

        return Ok(result);
    }

    [Authorize]
    [HttpDelete("{commentId}")]
    public async Task<IActionResult> DeleteComment([FromRoute] string commentId, CancellationToken ct)
    {
        await commentsService.DeleteCommentAsync(commentId, ct);

        return NoContent();
    }

    [Authorize]
    [HttpPost("{commentId}/support")]
    public async Task<IActionResult> AddSupportReaction([FromRoute] string commentId, CancellationToken ct)
    {
        await commentsService.AddReactionAsync(commentId, ct);

        return NoContent();
    }

    [Authorize]
    [HttpPost("{commentId}/remove-support")]
    public async Task<IActionResult> RemoveSupportReaction([FromRoute] string commentId, CancellationToken ct)
    {
        await commentsService.RemoveReactionAsync(commentId, ct);

        return NoContent();
    }
}
