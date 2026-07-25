namespace EbenezerBackend.Features.Comments.Domain.Entities;

public class CommentsEntity(
    string content,
    string authorId,
    IReadOnlyCollection<string> reactedBy,
    int commentsCount,
    DateTime createdAt,
    DateTime? updatedAt = null,
    DateTime? deletedAt = null,
    string? id = null
    )
{
    public string? Id { get; set; } = id;
    public string Content { get; set; } = content;
    public string AuthorId { get; set; } = authorId;
    public IReadOnlyCollection<string> ReactedBy { get; set; } = reactedBy;
    public int CommentsCount { get; set; } = commentsCount;
    public DateTime CreatedAt { get; set; } = createdAt;
    public DateTime? UpdatedAt { get; set; } = updatedAt;
    public DateTime? DeletedAt { get; set; } = deletedAt;
}