using EbenezerBackend.Features.Auth.Domain.Entities;
using EbenezerBackend.Shared.CustomAttributes;
using Newtonsoft.Json;

namespace EbenezerBackend.Features.Auth.Data.Models;

[CollectionName("Users")]
public class UserModel
{
    [JsonProperty("_key")]
    public required string Id { get; set; }

    public required string UserName { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string FullName { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required bool IsDeleted { get; set; }
    
    public static UserModel FromEntity(UserEntity entity) => new() {
        Id = entity.Id,
        UserName = entity.UserName!,
        FullName = entity.FullName,
        Email = entity.Email!,
        PasswordHash = entity.PasswordHash!,
        CreatedAt = entity.CreatedAt,
        IsDeleted = entity.IsDeleted
    };

    public UserEntity ToEntity() => new() {
        Id = this.Id,
        UserName = this.UserName,
        FullName = this.FullName,
        Email =  this.Email,
        PasswordHash = this.PasswordHash,
        CreatedAt = this.CreatedAt,
        IsDeleted = this.IsDeleted
    };
}