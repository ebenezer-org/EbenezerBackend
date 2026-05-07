using System.Threading.Tasks;

namespace EbenezerBackend.Features.Auth.Domain.Services.Interfaces;

public interface IAuthService
{
    Task<string> LoginAsync(string userName, string password);
    Task<string> RegisterAsync(string userName, string fullName, string email, string password);
}