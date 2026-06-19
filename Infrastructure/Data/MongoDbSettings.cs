namespace EbenezerBackend.Infrastructure.Data;

public class MongoDbSettings
{
    public string ConnectionString { get; init; } = GetVar("MONGODB_CONNECTION_STRING");
    public string DatabaseName { get; init; } = GetVar("MONGODB_DATABASE_NAME");

    private static string GetVar(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Missing environment variable: {name}");
}
