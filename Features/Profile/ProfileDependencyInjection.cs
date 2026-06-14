using EbenezerBackend.Features.Profile.Data;
using EbenezerBackend.Features.Profile.Domain.Repositories;
using EbenezerBackend.Features.Profile.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EbenezerBackend.Features.Profile;

public static class ProfileDependencyInjection
{
    public static IServiceCollection AddProfileModule(this IServiceCollection services)
    {
        services.AddScoped<IProfileService, ProfileService>().AddScoped<IProfileRepository, ProfileRepository>();
        
        return services;
    }
}