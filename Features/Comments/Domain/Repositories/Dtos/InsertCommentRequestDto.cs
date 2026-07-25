using EbenezerBackend.Features.Comments.Domain.Enums;

namespace EbenezerBackend.Features.Comments.Domain.Repositories.Dtos;

public record InsertCommentRequestDto(
    string AuthorId,
    string Content,
    string ParentId,
    CommentParentTypeEnum ParentType
);
