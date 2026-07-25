using EbenezerBackend.Shared.Web.Dtos;

namespace EbenezerBackend.Features.Comments.Presentation.Dtos.Shared;

public record CommentDto(
    string Id,
    UserEssentialDto Author,
    string Content,
    DateTime CreatedAt,
    IReadOnlyCollection<string> ReactedBy,
    int CommentCount
    );