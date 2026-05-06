using EbenezerBackend.Features.Auth.Data;
using EbenezerBackend.Features.Auth.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace EbenezerBackend.Infrastructure.Extensions.ServiceCollection;

public static class IdentityServiceExtensions
{
    public static IServiceCollection AddCustomIdentity(this IServiceCollection services)
    {
        services.AddIdentityCore<UserEntity>()
            .AddUserStore<AuthRepository>()
            .AddDefaultTokenProviders();

        return services;
    }
}