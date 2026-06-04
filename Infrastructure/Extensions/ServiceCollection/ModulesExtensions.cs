using EbenezerBackend.Features.Auth;
using EbenezerBackend.Features.Prayers;
using EbenezerBackend.Features.Profile;

namespace EbenezerBackend.Infrastructure.Extensions.ServiceCollection;

public static class ModulesExtensions
{
    public static IServiceCollection AddModules(this IServiceCollection services)
    {
        services
            .AddAuthModule()
            .AddProfileModule()
            .AddPrayersModule();
        
        return services;
    }
}