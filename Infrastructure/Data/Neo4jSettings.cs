namespace EbenezerBackend.Infrastructure.Data;

public class Neo4JSettings
{
    public const string DefaultDatabase = "neo4j";

    public string Uri      { get; init; } = GetVar("NEO4J_URI");
    public string User     { get; init; } = GetVar("NEO4J_USER");
    public string Password { get; init; } = GetVar("NEO4J_PASSWORD");

    public string Database { get; init; } = ResolveDatabase(Environment.GetEnvironmentVariable("NEO4J_DATABASE"));

    // Parâmetros do pool do driver. Os valores padrão são exatamente os padrões do Neo4j.Driver
    // (pool de 100 conexões, 60 s de espera por conexão e 30 s de retry de transação), para que
    // a comparação entre as abordagens use o padrão de cada biblioteca de acesso a dados.
    // Cada valor pode ser sobrescrito por variável de ambiente para experimentos específicos.
    public int MaxConnectionPoolSize { get; init; } = GetInt("NEO4J_MAX_CONNECTION_POOL_SIZE", 100);
    public int ConnectionAcquisitionTimeoutSeconds { get; init; } = GetInt("NEO4J_CONNECTION_ACQUISITION_TIMEOUT_SECONDS", 60);
    public int MaxTransactionRetryTimeSeconds { get; init; } = GetInt("NEO4J_MAX_TRANSACTION_RETRY_SECONDS", 30);

    private static int GetInt(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var value) && value > 0 ? value : fallback;

    internal static string ResolveDatabase(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? DefaultDatabase : raw.Trim();

    private static string GetVar(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Missing environment variable: {name}");
}
