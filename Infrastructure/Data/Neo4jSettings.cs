namespace EbenezerBackend.Infrastructure.Data;

public class Neo4JSettings
{
    public const string DefaultDatabase = "neo4j";

    public string Uri      { get; init; } = GetVar("NEO4J_URI");
    public string User     { get; init; } = GetVar("NEO4J_USER");
    public string Password { get; init; } = GetVar("NEO4J_PASSWORD");

    public string Database { get; init; } = ResolveDatabase(Environment.GetEnvironmentVariable("NEO4J_DATABASE"));

    // Parâmetros do pool do driver, explícitos para fazer parte do desenho do experimento.
    // O pool padrão do driver (100) é mantido; o prazo de aquisição cai de 60 s (padrão) para 10 s,
    // para a saturação falhar rápido em vez de segurar as requisições até esgotar os sockets do cliente.
    // Para repetir o teste com os padrões do driver: NEO4J_CONNECTION_ACQUISITION_TIMEOUT_SECONDS=60
    // e NEO4J_MAX_TRANSACTION_RETRY_SECONDS=30.
    public int MaxConnectionPoolSize { get; init; } = GetInt("NEO4J_MAX_CONNECTION_POOL_SIZE", 100);
    public int ConnectionAcquisitionTimeoutSeconds { get; init; } = GetInt("NEO4J_CONNECTION_ACQUISITION_TIMEOUT_SECONDS", 10);
    public int MaxTransactionRetryTimeSeconds { get; init; } = GetInt("NEO4J_MAX_TRANSACTION_RETRY_SECONDS", 5);

    private static int GetInt(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var value) && value > 0 ? value : fallback;

    internal static string ResolveDatabase(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? DefaultDatabase : raw.Trim();

    private static string GetVar(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Missing environment variable: {name}");
}
