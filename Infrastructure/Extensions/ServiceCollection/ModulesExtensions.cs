using EbenezerBackend.Features.Auth;
using EbenezerBackend.Features.Categories;
using EbenezerBackend.Features.Friendships;
using EbenezerBackend.Features.Prayers;
using EbenezerBackend.Features.Profile;
using Microsoft.Extensions.DependencyInjection;

namespace EbenezerBackend.Infrastructure.Extensions.ServiceCollection;

public static class ModulesExtensions
{
    public static IServiceCollection AddModules(this IServiceCollection services)
    {
        services
            .AddAuthModule()
            .AddProfileModule()
            .AddCategoriesModule()
            .AddPrayersModule()
            .AddFriendshipsModule();
        
        return services;
    }
}
