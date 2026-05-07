using System.Threading.Tasks;
using EbenezerBackend.Features.Auth.Domain.Entities;

namespace EbenezerBackend.Features.Auth.Domain.Repositories.Interfaces;

public interface IAuthRepository
{
    Task<UserEntity> RegisterUserAsync(UserEntity user);
    Task<UserEntity?> FindByUserName(string userName);
    Task<bool> UserExistsByUserNameOrEmail(string userName, string email);
}