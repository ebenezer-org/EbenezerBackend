using EbenezerBackend.Features.Comments.Domain.Enums;
using EbenezerBackend.Features.Comments.Domain.Exceptions;
using EbenezerBackend.Features.Comments.Domain.Repositories;
using EbenezerBackend.Features.Comments.Domain.Repositories.Dtos;
using EbenezerBackend.Features.Comments.Presentation.Dtos.PostComment;
using EbenezerBackend.Features.Comments.Presentation.Dtos.Shared;
using EbenezerBackend.Features.Comments.Presentation.Dtos.UpdateComment;
using EbenezerBackend.Shared.Web.Dtos;
using EbenezerBackend.Shared.Web.Dtos.Pagination;
using EbenezerBackend.Shared.Web.Services.UserContext;

namespace EbenezerBackend.Features.Comments.Domain.Services;

public class CommentsService(ICommentsRepository commentsRepository, IUserContext userContext) : ICommentsService
{
    public async Task<PaginatedResponseDto<CommentDto>> GetCommentsAsync(
        string parentId,
        CommentParentTypeEnum parentType,
        PaginationRequestDto pagination,
        CancellationToken ct)
    {
        var safePage = pagination.Page;
        var safePageSize = pagination.PageSize;

        var (items, totalCount) = await commentsRepository.ListDirectCommentsAsync(parentId, parentType, safePage, safePageSize, ct);

        var dtos = items.Select(ToCommentDto).ToList();

        return new PaginatedResponseDto<CommentDto>(dtos, safePage, safePageSize, totalCount);
    }

    public async Task<PostCommentResponseDto> PostCommentAsync(
        string parentId,
        CommentParentTypeEnum parentType,
        PostCommentRequestDto request,
        CancellationToken ct)
    {
        var authorId = userContext.Id;

        var insertRequest = new InsertCommentRequestDto(authorId, request.Content, parentId, parentType);
        var result = await commentsRepository.InsertCommentAsync(insertRequest, ct);

        return new PostCommentResponseDto(ToCommentDto(result));
    }

    public async Task<UpdateCommentResponseDto> UpdateCommentAsync(
        string commentId,
        UpdateCommentRequestDto request,
        CancellationToken ct)
    {
        var userId = userContext.Id;

        var updated = await commentsRepository.UpdateCommentAsync(commentId, userId, request.Content, ct);

        if (updated is null)
        {
            var existing = await commentsRepository.FindCommentByIdAsync(commentId, ct);
            if (existing is null)
                throw new CommentNotFoundException();

            throw new CommentAccessDeniedException();
        }

        return new UpdateCommentResponseDto(ToCommentDto(updated));
    }

    public async Task DeleteCommentAsync(string commentId, CancellationToken ct)
    {
        var userId = userContext.Id;

        var deleted = await commentsRepository.DeleteCommentAsync(commentId, userId, ct);

        if (!deleted)
        {
            var existing = await commentsRepository.FindCommentByIdAsync(commentId, ct);
            if (existing is null)
                throw new CommentNotFoundException();

            throw new CommentAccessDeniedException();
        }
    }

    public async Task AddReactionAsync(string commentId, CancellationToken ct)
    {
        await commentsRepository.AddReactionAsync(commentId, userContext.UserName, ct);
    }

    public async Task RemoveReactionAsync(string commentId, CancellationToken ct)
    {
        await commentsRepository.RemoveReactionAsync(commentId, userContext.UserName, ct);
    }

    private static CommentDto ToCommentDto(CommentRepositoryResponseDto repositoryDto)
    {
        var author = repositoryDto.AuthorProfileModel.ToEntity();
        var comment = repositoryDto.CommentModel.ToEntity();

        var authorDto = new UserEssentialDto(author.Id!, author.UserName, author.FullName);

        return new CommentDto(
            Id: comment.Id ?? string.Empty,
            Author: authorDto,
            Content: comment.Content,
            CreatedAt: comment.CreatedAt,
            ReactedBy: comment.ReactedBy,
            CommentCount: repositoryDto.DirectCommentsCount
        );
    }
}
