using EbenezerBackend.Features.Retrospective.Domain.Entities;
using EbenezerBackend.Features.Retrospective.Domain.Enums;
using EbenezerBackend.Infrastructure.Data;
using EbenezerBackend.Shared.CustomAttributes;
using EbenezerBackend.Shared.Data;

namespace EbenezerBackend.Features.Retrospective.Data.Models;

[CollectionName(ArangoDbCollections.EncouragementMessages)]
public class EncouragementMessageModel(
    EncouragementMessageCategoryEnum category,
    string title,
    string message,
    string scriptureVerse,
    string scriptureReference
    ) : ArangoDbBaseModel, IBaseModel<EncouragementMessageModel, EncouragementMessageEntity>
{
    public EncouragementMessageCategoryEnum Category { get; set; } = category;
    public string Title { get; set; } = title;
    public string Message { get; set; } = message;
    public string ScriptureVerse { get; set; } = scriptureVerse;
    public string ScriptureReference { get; set; } = scriptureReference;
    
    public EncouragementMessageEntity ToEntity()
    {
        return new EncouragementMessageEntity(Category, Title, Message, ScriptureVerse, ScriptureReference);
    }

    public static EncouragementMessageModel FromEntity(EncouragementMessageEntity entity)
    {
        return new EncouragementMessageModel(entity.Category, entity.Title, entity.Message, entity.ScriptureVerse, entity.ScriptureReference)
        {
            Key = entity.Category.ToString()
        };
    }
}