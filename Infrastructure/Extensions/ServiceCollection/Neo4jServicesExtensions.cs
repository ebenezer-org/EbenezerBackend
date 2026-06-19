using EbenezerBackend.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Neo4j.Driver;

namespace EbenezerBackend.Infrastructure.Extensions.ServiceCollection;

public static class Neo4JServicesExtensions
{
    public static IServiceCollection AddNeo4JDb(this IServiceCollection services)
    {
        var settings = new Neo4JSettings();
        services.AddSingleton(settings);

        var driver = GraphDatabase.Driver(
            settings.Uri,
            AuthTokens.Basic(settings.User, settings.Password)
        );
        services.AddSingleton<IDriver>(driver);

        return services;
    }
}
