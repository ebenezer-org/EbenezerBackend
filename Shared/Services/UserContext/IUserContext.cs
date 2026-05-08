namespace EbenezerBackend.Shared.Services.UserContext;

public interface IUserContext
{
    string Id { get; }
    string UserName { get; }
    string Email { get; }
}