using System;
using EbenezerBackend.Features.Auth.Domain.Entities;
using EbenezerBackend.Infrastructure.Data;
using EbenezerBackend.Shared.CustomAttributes;
using EbenezerBackend.Shared.Data;
using MongoDB.Bson.Serialization.Attributes;

namespace EbenezerBackend.Features.Auth.Data.Models;

[CollectionName(DbCollections.Users)]
[BsonIgnoreExtraElements]
public class AuthUserModel : IBaseModel<AuthUserModel, AuthUserEntity>
{
    [BsonId]
    public string? Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string NormalizedUserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public static AuthUserModel FromEntity(AuthUserEntity entity) => new()
    {
        Id = entity.Id,
        UserName = entity.UserName!,
        NormalizedUserName = entity.UserName!.ToUpperInvariant(),
        Email = entity.Email!,
        NormalizedEmail = entity.Email!.ToUpperInvariant(),
        PasswordHash = entity.PasswordHash!,
        CreatedAt = entity.CreatedAt,
        IsDeleted = entity.IsDeleted
    };

    public AuthUserEntity ToEntity() => new()
    {
        Id = this.Id!,
        UserName = this.UserName,
        Email = this.Email,
        PasswordHash = this.PasswordHash,
        CreatedAt = this.CreatedAt,
        IsDeleted = this.IsDeleted
    };
}
