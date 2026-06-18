using System;
using EbenezerBackend.Features.Prayers.Domain.Entities;
using EbenezerBackend.Features.Prayers.Domain.Enums;
using EbenezerBackend.Infrastructure.Data;
using EbenezerBackend.Shared.CustomAttributes;
using EbenezerBackend.Shared.Data;

namespace EbenezerBackend.Features.Prayers.Data.Models;

[CollectionName(DbCollections.Prayers)]
public class PrayerModel(string content, DateTime createdAt, bool isPublic) : IBaseModel<PrayerModel, PrayerEntity>
{
    public string? Key { get; set; }
    public string Content { get; set; } = content;
    public bool IsPublic { get; set; } = isPublic;
    public DateTime CreatedAt { get; init; } = createdAt;
    public DateTime UpdatedAt { get; set; } = createdAt;
    public PrayerAnswerStatusEnum? DivineAnswerStatus { get; set; }
    public string? AuthorResponseMessage { get; set; }
    public DateTime? AuthorResponseCreatedAt { get; set; }

    public PrayerEntity ToEntity()
        => new(
            Content,
            IsPublic,
            Key,
            CreatedAt,
            UpdatedAt,
            DivineAnswerStatus,
            AuthorResponseMessage,
            AuthorResponseCreatedAt);

    public static PrayerModel FromEntity(PrayerEntity entity)
    {
        return new PrayerModel(entity.Content, entity.CreatedAt, entity.IsPublic)
        {
            Key = entity.Id,
            UpdatedAt = entity.UpdatedAt,
            DivineAnswerStatus = entity.AuthorResponseStatus,
            AuthorResponseMessage = entity.AuthorResponseMessage,
            AuthorResponseCreatedAt = entity.AuthorResponseCreatedAt
        };
    }
}
