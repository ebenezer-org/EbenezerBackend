using EbenezerBackend.Features.Comments.Domain.Enums;
using EbenezerBackend.Features.Comments.Presentation.Dtos.PostComment;
using EbenezerBackend.Features.Comments.Presentation.Dtos.Shared;
using EbenezerBackend.Features.Comments.Presentation.Dtos.UpdateComment;
using EbenezerBackend.Shared.Web.Dtos.Pagination;

namespace EbenezerBackend.Features.Comments.Domain.Services;

public interface ICommentsService
{
    Task<PaginatedResponseDto<CommentDto>> GetCommentsAsync(string parentId, CommentParentTypeEnum parentType, PaginationRequestDto pagination, CancellationToken ct);

    Task<PostCommentResponseDto> PostCommentAsync(string parentId, CommentParentTypeEnum parentType, PostCommentRequestDto request, CancellationToken ct);

    Task<UpdateCommentResponseDto> UpdateCommentAsync(string commentId, UpdateCommentRequestDto request, CancellationToken ct);

    Task DeleteCommentAsync(string commentId, CancellationToken ct);

    Task AddReactionAsync(string commentId, CancellationToken ct);

    Task RemoveReactionAsync(string commentId, CancellationToken ct);
}
