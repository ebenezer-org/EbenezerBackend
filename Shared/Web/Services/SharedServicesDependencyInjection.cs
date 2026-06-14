using EbenezerBackend.Shared.Web.Services.EnsureUserExists;
using EbenezerBackend.Shared.Web.Services.UserContext;

namespace EbenezerBackend.Shared.Web.Services;

public static class SharedServicesDependencyInjection
{
    public static IServiceCollection AddSharedServices(this IServiceCollection services)
    {
        services
            .AddHttpContextAccessor()
            .AddScoped<IUserContext, UserContext.UserContext>()
            .AddScoped<IEnsureUserExistsService, EnsureUserExistsService>();
        
        return services;
    }
}