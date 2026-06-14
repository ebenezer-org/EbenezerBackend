using System;
using EbenezerBackend.Features.Auth.Domain.Entities;
using EbenezerBackend.Infrastructure.Data;
using EbenezerBackend.Shared.CustomAttributes;
using EbenezerBackend.Shared.Data;
using Newtonsoft.Json;

namespace EbenezerBackend.Features.Auth.Data.Models;

[CollectionName(ArangoDbCollections.Users)]
public class AuthUserModel : ArangoDbBaseModel
{
    public required string UserName { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required bool IsDeleted { get; set; }
    
    public static AuthUserModel FromEntity(AuthUserEntity entity) => new() {
        Key = entity.Id,
        UserName = entity.UserName!,
        Email = entity.Email!,
        PasswordHash = entity.PasswordHash!,
        CreatedAt = entity.CreatedAt,
        IsDeleted = entity.IsDeleted
    };

    public AuthUserEntity ToEntity() => new() {
        Id = this.Key!,
        UserName = this.UserName,
        Email =  this.Email,
        PasswordHash = this.PasswordHash,
        CreatedAt = this.CreatedAt,
        IsDeleted = this.IsDeleted
    };
}