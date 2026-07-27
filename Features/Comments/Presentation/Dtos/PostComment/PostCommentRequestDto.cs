using EbenezerBackend.Features.Comments.Domain.Enums;

namespace EbenezerBackend.Features.Comments.Presentation.Dtos.PostComment;

public record PostCommentRequestDto(
    string Content
    );