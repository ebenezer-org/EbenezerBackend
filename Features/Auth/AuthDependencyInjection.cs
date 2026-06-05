using EbenezerBackend.Features.Auth.Data;
using EbenezerBackend.Features.Auth.Domain.Repositories;
using EbenezerBackend.Features.Auth.Domain.Services.Implementations;
using EbenezerBackend.Features.Auth.Domain.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace EbenezerBackend.Features.Auth;

public static class AuthDependencyInjection
{
    public static IServiceCollection AddAuthModule(this IServiceCollection services)
    {
        services
            .AddScoped<IAuthService, AuthService>()
            .AddScoped<IAuthRepository, AuthRepository>()
            .AddScoped<ITokenService, TokenService>();
        
        return services;
    }
}