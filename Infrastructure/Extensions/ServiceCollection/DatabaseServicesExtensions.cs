using ArangoDBNetStandard;
using ArangoDBNetStandard.Transport.Http;
using EbenezerBackend.Infrastructure.Data;

namespace EbenezerBackend.Infrastructure.Extensions.ServiceCollection;

public static class DatabaseServicesExtensions
{
    public static IServiceCollection AddArangoDb(this IServiceCollection services, IConfiguration configuration)
    {
        var arangoSettings = configuration.GetSection("ArangoDb").Get<ArangoDbSettings>()
                             ?? new ArangoDbSettings();
        services.AddSingleton(arangoSettings);

                
        var transport = HttpApiTransport.UsingBasicAuth(
                            new Uri($"{arangoSettings.Protocol}://{arangoSettings.Host}:{arangoSettings.Port}"),
                            arangoSettings.DatabaseName,
                            arangoSettings.User,
                            arangoSettings.Password
                        );
        
        services.AddSingleton<IArangoDBClient>(new ArangoDBClient(transport));
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