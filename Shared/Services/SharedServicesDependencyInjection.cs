using EbenezerBackend.Shared.Services.UserContext;
using Microsoft.Extensions.DependencyInjection;

namespace EbenezerBackend.Shared.Services;

public static class SharedServicesDependencyInjection
{
    public static IServiceCollection AddSharedServices(this IServiceCollection services)
    {
        services
            .AddHttpContextAccessor()
            .AddScoped<IUserContext, UserContext.UserContext>();
        
        return services;
    }
}