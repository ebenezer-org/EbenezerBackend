using EbenezerBackend.Features.Comments.Domain.Entities;
using EbenezerBackend.Infrastructure.Data;
using EbenezerBackend.Shared.CustomAttributes;
using EbenezerBackend.Shared.Data;

namespace EbenezerBackend.Features.Comments.Data.Models;

[CollectionName(ArangoDbCollections.Comments)]
public class CommentsModel : ArangoDbBaseModel, IBaseModel<CommentsModel, CommentsEntity>
{
    public required string Content { get; set; }
    public required string AuthorId { get; set; }
    public required IReadOnlyCollection<string> ReactedBy { get; set; }
    public required int CommentsCount { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public static CommentsModel FromEntity(CommentsEntity entity) => new()
    {
        Key = entity.Id,
        Content = entity.Content,
        AuthorId = entity.AuthorId,
        ReactedBy = entity.ReactedBy,
        CommentsCount = entity.CommentsCount,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
        DeletedAt = entity.DeletedAt
    };

    public CommentsEntity ToEntity() => new(
        content: Content,
        authorId: AuthorId,
        reactedBy: ReactedBy,
        commentsCount: CommentsCount,
        createdAt: CreatedAt,
        updatedAt: UpdatedAt,
        deletedAt: DeletedAt,
        id: Key
    );
}