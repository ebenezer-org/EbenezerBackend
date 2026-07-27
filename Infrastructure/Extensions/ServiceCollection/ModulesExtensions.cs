using EbenezerBackend.Features.Auth;
using EbenezerBackend.Features.Categories;
using EbenezerBackend.Features.Comments;
using EbenezerBackend.Features.Friendships;
using EbenezerBackend.Features.Prayers;
using EbenezerBackend.Features.Profile;
using EbenezerBackend.Features.Retrospective;
using Microsoft.Extensions.DependencyInjection;

namespace EbenezerBackend.Infrastructure.Extensions.ServiceCollection;

public static class ModulesExtensions
{
    public static IServiceCollection AddModules(this IServiceCollection services)
    {
        services
            .AddAuthModule()
            .AddProfileModule()
            .AddPrayersModule()
            .AddCategoriesModule()
            .AddCommentsModule()
            .AddFriendshipsModule()
            .AddRetrospectiveModule();
        
        return services;
    }
}
