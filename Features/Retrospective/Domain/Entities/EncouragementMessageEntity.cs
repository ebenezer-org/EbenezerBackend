using EbenezerBackend.Features.Retrospective.Domain.Enums;

namespace EbenezerBackend.Features.Retrospective.Domain.Entities;

public class EncouragementMessageEntity(
    EncouragementMessageCategoryEnum category,
    string title,
    string message,
    string scriptureVerse,
    string scriptureReference
    )
{
    public EncouragementMessageCategoryEnum Category { get; } = category;
    public string Title { get; } = title;
    public string Message { get; } = message;
    public string ScriptureVerse { get; } = scriptureVerse;
    public string ScriptureReference { get; } = scriptureReference;
}