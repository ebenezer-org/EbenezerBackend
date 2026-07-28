using EbenezerBackend.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace EbenezerBackend.Infrastructure.Extensions.ServiceCollection;

public static class MongoDbServicesExtensions
{
    public static IServiceCollection AddMongoDb(this IServiceCollection services)
    {
        var settings = new MongoDbSettings();
        services.AddSingleton(settings);

        var client = new MongoClient(settings.ConnectionString);
        services.AddSingleton<IMongoClient>(client);

        var database = client.GetDatabase(settings.DatabaseName);
        services.AddSingleton<IMongoDatabase>(database);

        services.AddSingleton<MongoDbInitializer>();

        return services;
    }
}
