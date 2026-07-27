using System.Text.Json.Serialization;

namespace EbenezerBackend.Features.Comments.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CommentParentTypeEnum
{
    Prayer,
    Comment
}