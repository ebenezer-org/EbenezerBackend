namespace EbenezerBackend.Features.Auth.Domain.Services.Interfaces;

public interface IAuthService
{
    Task<string> LoginAsync(string userName, string password);
    Task<string> RegisterAsync(string userName, string email, string password);
}