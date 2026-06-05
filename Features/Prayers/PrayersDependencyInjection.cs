using EbenezerBackend.Features.Prayers.Data;
using EbenezerBackend.Features.Prayers.Domain.Repositories;
using EbenezerBackend.Features.Prayers.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EbenezerBackend.Features.Prayers;

public static class PrayersDependencyInjection
{
    public static IServiceCollection AddPrayersModule(this IServiceCollection services)
    {
        services
            .AddScoped<IPrayersService, PrayersService>()
            .AddScoped<IPrayersRepository, PrayersRepository>();
        
        return services;
    }
}