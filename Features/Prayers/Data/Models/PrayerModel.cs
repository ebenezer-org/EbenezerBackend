using System;
using EbenezerBackend.Features.Prayers.Domain.Entities;
using EbenezerBackend.Infrastructure.Data;
using EbenezerBackend.Shared.CustomAttributes;

namespace EbenezerBackend.Features.Prayers.Data.Models;

[CollectionName("Prayers")]
public class PrayerModel(string content, DateTime createdAt) : ArangoDbBaseModel, IBaseModel<PrayerModel, PrayerEntity>
{
    public string Content { get; set; } = content;
    public DateTime CreatedAt { get; init; } = createdAt;
    
    public PrayerEntity ToEntity() => new(Content) { CreatedAt = CreatedAt };

    public static PrayerModel FromEntity(PrayerEntity entity)
    {
        return new PrayerModel(entity.Content, entity.CreatedAt);
    }
}
