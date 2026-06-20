using System;
using EbenezerBackend.Features.Categories.Domain.Entities;
using EbenezerBackend.Infrastructure.Data;
using EbenezerBackend.Shared.CustomAttributes;
using EbenezerBackend.Shared.Data;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EbenezerBackend.Features.Categories.Data.Models;

[CollectionName(DbCollections.Categories)]
[BsonIgnoreExtraElements]
public class CategoryModel : IBaseModel<CategoryModel, CategoryEntity>
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string OwnerUsername { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ColorHex { get; set; } = string.Empty;
    public bool IsPublic { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public CategoryEntity ToEntity()
    {
        return new CategoryEntity(OwnerUsername, Name, Description, ColorHex, IsPublic, Id, CreatedAt, UpdatedAt);
    }

    public static CategoryModel FromEntity(CategoryEntity entity)
    {
        return new CategoryModel
        {
            Id = entity.Id,
            OwnerUsername = entity.OwnerUsername,
            Name = entity.Name,
            Description = entity.Description,
            ColorHex = entity.ColorHex,
            IsPublic = entity.IsPublic,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
