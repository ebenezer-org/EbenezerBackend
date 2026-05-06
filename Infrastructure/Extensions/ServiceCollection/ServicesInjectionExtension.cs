using EbenezerBackend.Features.Auth.Data;
using EbenezerBackend.Features.Auth.Domain.Repositories.Interfaces;
using EbenezerBackend.Features.Auth.Domain.Services.Implementations;
using EbenezerBackend.Features.Auth.Domain.Services.Interfaces;

namespace EbenezerBackend.Infrastructure.Extensions.ServiceCollection;

public static class ServicesInjectionExtension
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>().AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<ITokenService, TokenService>();
        
        return services;
    }
}