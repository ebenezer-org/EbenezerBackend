using System;
using EbenezerBackend.Features.Categories.Domain.Entities;
using EbenezerBackend.Infrastructure.Data;
using EbenezerBackend.Shared.CustomAttributes;
using EbenezerBackend.Shared.Data;

namespace EbenezerBackend.Features.Categories.Data.Models;

[CollectionName(ArangoDbCollections.Categories)]
public class CategoryModel : ArangoDbBaseModel, IBaseModel<CategoryModel, CategoryEntity>
{
    public string OwnerUsername { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ColorHex { get; set; } = string.Empty;
    public bool IsPublic { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public CategoryEntity ToEntity()
    {
        return new CategoryEntity(OwnerUsername, Name, Description, ColorHex, IsPublic, Key, CreatedAt, UpdatedAt);
    }

    public static CategoryModel FromEntity(CategoryEntity entity)
    {
        return new CategoryModel
        {
            Key = entity.Id,
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
