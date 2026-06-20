namespace EbenezerBackend.Infrastructure.Data;

public class Neo4JSettings
{
    public const string DefaultDatabase = "neo4j";

    public string Uri      { get; init; } = GetVar("NEO4J_URI");
    public string User     { get; init; } = GetVar("NEO4J_USER");
    public string Password { get; init; } = GetVar("NEO4J_PASSWORD");

    public string Database { get; init; } = ResolveDatabase(Environment.GetEnvironmentVariable("NEO4J_DATABASE"));

    internal static string ResolveDatabase(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? DefaultDatabase : raw.Trim();

    private static string GetVar(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Missing environment variable: {name}");
}
