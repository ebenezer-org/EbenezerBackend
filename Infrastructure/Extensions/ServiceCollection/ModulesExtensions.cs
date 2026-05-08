using EbenezerBackend.Features.Auth;
using EbenezerBackend.Features.Profile;
using Microsoft.Extensions.DependencyInjection;

namespace EbenezerBackend.Infrastructure.Extensions.ServiceCollection;

public static class ModulesExtensions
{
    public static IServiceCollection AddModules(this IServiceCollection services)
    {
        services
            .AddAuthModule()
            .AddProfileModule();
        
        return services;
    }
}