using EbenezerBackend.Features.Profile.Domain.Entities;
using EbenezerBackend.Infrastructure.Data;
using EbenezerBackend.Shared.CustomAttributes;
using EbenezerBackend.Shared.Data;

namespace EbenezerBackend.Features.Profile.Data.Models;

[CollectionName(ArangoDbCollections.Users)]
public class ProfileModel( string userName, string fullName, string bio, string phone) : ArangoDbBaseModel, IBaseModel<ProfileModel, ProfileEntity>
{
    public readonly string UserName = userName;
    public readonly string FullName = fullName;
    public readonly string Bio = bio;
    public readonly string Phone = phone;

    public ProfileEntity ToEntity() => new(id: Key, username: UserName,fullName: FullName,bio: Bio,phone: Phone);

    public static ProfileModel FromEntity(ProfileEntity entity) => new(entity.UserName, entity.FullName, entity.Bio, entity.Phone) 
    {
        Key = entity.Id
    };
}