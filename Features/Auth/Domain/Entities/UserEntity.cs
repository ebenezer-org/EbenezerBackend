using Microsoft.AspNetCore.Identity;

namespace EbenezerBackend.Features.Auth.Domain.Entities;


public class UserEntity : IdentityUser
{
    public override required string? UserName { get; set; }
    public required string FullName { get; set; }
    public override string? Email { get; set; }
    public override required string? PasswordHash { get; set; }
    public required DateTime CreatedAt { get; init; }
    public required bool IsDeleted { get; set; }
}