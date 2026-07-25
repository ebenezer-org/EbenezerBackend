using EbenezerBackend.Features.Comments.Domain.Enums;
using EbenezerBackend.Features.Comments.Domain.Repositories.Dtos;

namespace EbenezerBackend.Features.Comments.Domain.Repositories;

public interface ICommentsRepository
{
    Task<CommentRepositoryResponseDto> InsertCommentAsync(InsertCommentRequestDto request, CancellationToken ct);

    Task<(IReadOnlyCollection<CommentRepositoryResponseDto> Items, int TotalCount)> ListDirectCommentsAsync(
        string parentId,
        CommentParentTypeEnum parentType,
        int page,
        int pageSize,
        CancellationToken ct);

    Task<CommentRepositoryResponseDto?> FindCommentByIdAsync(string commentId, CancellationToken ct);

    Task<CommentRepositoryResponseDto?> UpdateCommentAsync(
        string commentId,
        string ownerId,
        string content,
        CancellationToken ct);

    Task<bool> DeleteCommentAsync(string commentId, string ownerId, CancellationToken ct);

    Task AddReactionAsync(string commentId, string reactorId, CancellationToken ct);

    Task RemoveReactionAsync(string commentId, string reactorId, CancellationToken ct);
}
