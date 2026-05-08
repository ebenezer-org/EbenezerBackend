using EbenezerBackend.Features.Profile.Domain.Entities;
using EbenezerBackend.Shared.CustomAttributes;

namespace EbenezerBackend.Features.Profile.Data.Models;

[CollectionName("Users")]
public class ProfileModel( string userName, string fullName, string bio, string phone)
{
    public readonly string UserName = userName;
    public readonly string FullName = fullName;
    public readonly string Bio = bio;
    public readonly string Phone = phone;

    public ProfileEntity ToEntity() => new(UserName, FullName, Bio, Phone);

    public static ProfileModel FromEntity(ProfileEntity entity) => new(entity.UserName, entity.FullName, entity.Bio, entity.Phone);
}