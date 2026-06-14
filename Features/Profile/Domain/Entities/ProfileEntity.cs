namespace EbenezerBackend.Features.Profile.Domain.Entities;

public class ProfileEntity(string username, string fullName, string bio, string phone, string? id = null)
{
    public string? Id { get; } = id;
    public readonly string UserName = username;
    public string FullName { get; private set;  } = fullName;
    public string Bio { get;  private set;  } = bio;
    public string Phone { get; private set;  } = phone;
}