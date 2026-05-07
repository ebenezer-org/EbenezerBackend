using System.Threading.Tasks;
using EbenezerBackend.Features.Auth.Domain.Entities;

namespace EbenezerBackend.Features.Auth.Domain.Repositories.Interfaces;

public interface IAuthRepository
{
    Task<AuthUserEntity> RegisterUserAsync(AuthUserEntity authUser);
    Task<AuthUserEntity?> FindByUserName(string userName);
    Task<bool> UserExistsByUserNameOrEmail(string userName, string email);
}