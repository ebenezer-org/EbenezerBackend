using EbenezerBackend.Features.Comments.Domain.Entities;
using EbenezerBackend.Features.Comments.Domain.Enums;
using EbenezerBackend.Infrastructure.Data;
using EbenezerBackend.Shared.CustomAttributes;
using EbenezerBackend.Shared.Data;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EbenezerBackend.Features.Comments.Data.Models;

[CollectionName(DbCollections.Comments)]
[BsonIgnoreExtraElements]
public class CommentsModel : IBaseModel<CommentsModel, CommentsEntity>
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string Content { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;

    /// <summary>
    /// Id do documento pai (oração ou comentário).
    /// </summary>
    public string ParentId { get; set; } = string.Empty;

    /// <summary>
    /// Tipo do pai: Prayer ou Comment.
    /// </summary>
    public CommentParentTypeEnum ParentType { get; set; }

    /// <summary>
    /// Usernames dos usuários que reagiram ao comentário (lista embutida).
    /// </summary>
    public List<string> ReactedBy { get; set; } = [];

    /// <summary>
    /// Contador de respostas diretas a este comentário (mantido por $inc).
    /// </summary>
    public int CommentsCount { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public CommentsEntity ToEntity() => new(
        content: Content,
        authorId: AuthorId,
        reactedBy: ReactedBy.AsReadOnly(),
        commentsCount: CommentsCount,
        createdAt: CreatedAt,
        updatedAt: UpdatedAt,
        deletedAt: DeletedAt,
        id: Id
    );

    public static CommentsModel FromEntity(CommentsEntity entity) => new()
    {
        Id = entity.Id,
        Content = entity.Content,
        AuthorId = entity.AuthorId,
        ReactedBy = entity.ReactedBy.ToList(),
        CommentsCount = entity.CommentsCount,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
        DeletedAt = entity.DeletedAt
    };
}
