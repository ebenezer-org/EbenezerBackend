using EbenezerBackend.Features.Profile.Domain.Entities;
using EbenezerBackend.Infrastructure.Data;
using EbenezerBackend.Shared.CustomAttributes;
using EbenezerBackend.Shared.Data;
using MongoDB.Bson.Serialization.Attributes;

namespace EbenezerBackend.Features.Profile.Data.Models;

[CollectionName(DbCollections.Users)]
[BsonIgnoreExtraElements]
public class ProfileModel : IBaseModel<ProfileModel, ProfileEntity>
{
    [BsonId]
    public string? Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    public ProfileEntity ToEntity() => new(username: UserName, fullName: FullName, bio: Bio, phone: Phone, id: Id);

    public static ProfileModel FromEntity(ProfileEntity entity) => new()
    {
        Id = entity.Id,
        UserName = entity.UserName,
        FullName = entity.FullName,
        Bio = entity.Bio,
        Phone = entity.Phone
    };
}
