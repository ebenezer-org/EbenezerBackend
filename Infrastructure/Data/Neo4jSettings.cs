namespace EbenezerBackend.Infrastructure.Data;

public class Neo4JSettings
{
    public string Uri      { get; init; } = GetVar("NEO4J_URI");
    public string User     { get; init; } = GetVar("NEO4J_USER");
    public string Password { get; init; } = GetVar("NEO4J_PASSWORD");

    private static string GetVar(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Missing environment variable: {name}");
}
