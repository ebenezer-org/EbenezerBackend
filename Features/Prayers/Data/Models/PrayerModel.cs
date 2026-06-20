using System;
using EbenezerBackend.Features.Prayers.Domain.Entities;
using EbenezerBackend.Features.Prayers.Domain.Enums;
using EbenezerBackend.Infrastructure.Data;
using EbenezerBackend.Shared.CustomAttributes;
using EbenezerBackend.Shared.Data;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EbenezerBackend.Features.Prayers.Data.Models;

[CollectionName(DbCollections.Prayers)]
[BsonIgnoreExtraElements]
public class PrayerModel : IBaseModel<PrayerModel, PrayerEntity>
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string AuthorUsername { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsPublic { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; set; }

    public List<string> CategoryIds { get; set; } = new();
    public List<PrayerSupportReactionModel> Supporters { get; set; } = new();

    [BsonRepresentation(BsonType.String)]
    public PrayerAnswerStatusEnum? DivineAnswerStatus { get; set; }

    public string? AuthorResponseMessage { get; set; }
    public DateTime? AuthorResponseCreatedAt { get; set; }

    public PrayerEntity ToEntity()
        => new(
            Content,
            IsPublic,
            Id,
            CreatedAt,
            UpdatedAt,
            DivineAnswerStatus,
            AuthorResponseMessage,
            AuthorResponseCreatedAt);

    public static PrayerModel FromEntity(PrayerEntity entity)
    {
        return new PrayerModel
        {
            Id = entity.Id,
            Content = entity.Content,
            IsPublic = entity.IsPublic,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            DivineAnswerStatus = entity.AuthorResponseStatus,
            AuthorResponseMessage = entity.AuthorResponseMessage,
            AuthorResponseCreatedAt = entity.AuthorResponseCreatedAt
        };
    }
}
