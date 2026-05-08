namespace EbenezerBackend.Features.Profile.Domain.Entities;

public class ProfileEntity( string userName, string fullName, string bio, string phone)
{
    public readonly string UserName = userName;
    public string FullName { get; private set;  } = fullName;
    public string Bio { get;  private set;  } = bio;
    public string Phone { get; private set;  } = phone;
}