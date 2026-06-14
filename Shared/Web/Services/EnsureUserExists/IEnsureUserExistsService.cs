namespace EbenezerBackend.Shared.Web.Services.EnsureUserExists;

public interface IEnsureUserExistsService
{
    Task EnsureExistsByIdAsync(string userId, CancellationToken ct);
    Task EnsureExistsByUsernameAsync(string username, CancellationToken ct);
}