using EbenezerBackend.Features.Retrospective.Domain.Entities;
using EbenezerBackend.Features.Retrospective.Domain.Enums;
using EbenezerBackend.Infrastructure.Data;
using EbenezerBackend.Shared.CustomAttributes;
using EbenezerBackend.Shared.Data;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EbenezerBackend.Features.Retrospective.Data.Models;

[CollectionName(DbCollections.EncouragementMessages)]
[BsonIgnoreExtraElements]
public class EncouragementMessageModel : IBaseModel<EncouragementMessageModel, EncouragementMessageEntity>
{
    [BsonId]
    public string? Id { get; set; }

    [BsonRepresentation(BsonType.String)]
    public EncouragementMessageCategoryEnum Category { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string ScriptureVerse { get; set; } = string.Empty;
    public string ScriptureReference { get; set; } = string.Empty;

    public EncouragementMessageEntity ToEntity()
    {
        return new EncouragementMessageEntity(Category, Title, Message, ScriptureVerse, ScriptureReference);
    }

    public static EncouragementMessageModel FromEntity(EncouragementMessageEntity entity)
    {
        return new EncouragementMessageModel
        {
            Id = entity.Category.ToString(),
            Category = entity.Category,
            Title = entity.Title,
            Message = entity.Message,
            ScriptureVerse = entity.ScriptureVerse,
            ScriptureReference = entity.ScriptureReference
        };
    }
}
