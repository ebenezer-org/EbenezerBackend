using EbenezerBackend.Shared.Data;

namespace EbenezerBackend.Infrastructure.Data;

/// <summary>
/// Garante as constraints/índices necessários no Neo4j para buscas eficientes pelos ids
/// dos nós de usuário e das arestas de amizade. Equivalente ao MongoDbInitializer para o grafo social.
/// </summary>
public class Neo4JDbInitializer(INeo4JExecutor executor, ILogger<Neo4JDbInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Iniciando verificação de índices do Neo4j.");

        await CreateConstraintsAsync(ct);

        logger.LogInformation("Neo4j pronto para uso.");
    }

    // ─── Constraints (com índice de suporte automático) ───────────────────────

    private async Task CreateConstraintsAsync(CancellationToken ct)
    {
        // Nós "pointer" de usuário são resolvidos por id em praticamente toda query de grafo social
        // (amizades, sugestões, visibilidade). Uma constraint de unicidade cria o índice de suporte
        // e evita nós duplicados quando o MERGE self-healing cria o nó sob demanda.
        await EnsureConstraintAsync(
            "user_id_unique",
            "CREATE CONSTRAINT user_id_unique IF NOT EXISTS FOR (u:User) REQUIRE u.id IS UNIQUE",
            ct);

        // O id da amizade é usado para localizar/atualizar/excluir uma solicitação específica
        // (FindFriendshipRequestByIdAsync, UpdateFriendshipRequestAsync, DeleteFriendshipRequestByIdAsync).
        await EnsureConstraintAsync(
            "friendship_id_unique",
            $"CREATE CONSTRAINT friendship_id_unique IF NOT EXISTS FOR ()-[f:{DbEdges.Friendships}]-() REQUIRE f.id IS UNIQUE",
            ct);
    }

    private async Task EnsureConstraintAsync(string name, string cypher, CancellationToken ct)
    {
        // CREATE CONSTRAINT ... IF NOT EXISTS não retorna registros, então SingleOrDefault
        // simplesmente devolve null tanto na criação quanto quando a constraint já existe.
        await executor.ExecuteWriteSingleOrDefaultAsync<object>(cypher, null, _ => new object(), ct);
        logger.LogInformation("Constraint '{Name}' garantida no Neo4j.", name);
    }
}
