using EbenezerBackend.Infrastructure.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EbenezerBackend.Infrastructure.Extensions;

public static class DependencyInjectionConfig
{
    public static IServiceCollection AddArangoDb(this IServiceCollection services, IConfiguration configuration)
    {
        var arangoSettings = configuration.GetSection("ArangoDb").Get<ArangoDbSettings>()
                             ?? new ArangoDbSettings();
        services.AddSingleton(arangoSettings);

        services.AddScoped<ArangoDbContext>();
        services.AddScoped<DatabaseInitializer>();

        return services;
    }

    public static async Task UseArangoDbInitialization(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        await initializer.InitializeAsync();
    }
}