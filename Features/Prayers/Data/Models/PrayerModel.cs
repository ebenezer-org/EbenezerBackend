using System;
using EbenezerBackend.Features.Prayers.Domain.Entities;
using EbenezerBackend.Infrastructure.Data;
using EbenezerBackend.Shared.CustomAttributes;

namespace EbenezerBackend.Features.Prayers.Data.Models;

[CollectionName("Prayers")]
public class PrayerModel(string content, DateTime createdAt, bool isPublic) : ArangoDbBaseModel, IBaseModel<PrayerModel, PrayerEntity>
{
    public string Content { get; set; } = content;
    public bool IsPublic { get; set; } = isPublic;
    public DateTime CreatedAt { get; init; } = createdAt;
    public DateTime UpdatedAt { get; set; } = createdAt;
    public string? AuthorResponseStatus { get; set; }
    public string? AuthorResponseMessage { get; set; }
    public DateTime? AuthorResponseCreatedAt { get; set; }

    public PrayerEntity ToEntity()
        => new(
            Content,
            IsPublic,
            Id,
            CreatedAt,
            UpdatedAt,
            AuthorResponseStatus,
            AuthorResponseMessage,
            AuthorResponseCreatedAt);

    public static PrayerModel FromEntity(PrayerEntity entity)
    {
        return new PrayerModel(entity.Content, entity.CreatedAt, entity.IsPublic)
        {
            Id = entity.Id,
            UpdatedAt = entity.UpdatedAt,
            AuthorResponseStatus = entity.AuthorResponseStatus,
            AuthorResponseMessage = entity.AuthorResponseMessage,
            AuthorResponseCreatedAt = entity.AuthorResponseCreatedAt
        };
    }
}
