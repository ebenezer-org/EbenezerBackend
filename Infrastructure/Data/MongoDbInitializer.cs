using EbenezerBackend.Features.Retrospective.Data.Models;
using EbenezerBackend.Features.Retrospective.Domain.Enums;
using EbenezerBackend.Shared.Data;
using MongoDB.Driver;

namespace EbenezerBackend.Infrastructure.Data;

/// <summary>
/// Inicializa o MongoDB com índices e dados seed necessários para o funcionamento da aplicação.
/// Equivalente ao DatabaseInitializer da abordagem ArangoDB.
/// </summary>
public class MongoDbInitializer(IMongoDatabase database, ILogger<MongoDbInitializer> logger)
{
    public async Task InitializeAsync()
    {
        logger.LogInformation("Iniciando verificação e seed do MongoDB.");

        await CreateIndexesAsync();
        await SeedEncouragementMessagesAsync();

        logger.LogInformation("MongoDB pronto para uso.");
    }

    // ─── Indexes ──────────────────────────────────────────────────────────────

    private async Task CreateIndexesAsync()
    {
        await EnsureUniqueIndexAsync<dynamic>(
            DbCollections.Users,
            Builders<dynamic>.IndexKeys.Ascending("UserName"),
            "idx_unique_username",
            unique: true);

        await EnsureIndexAsync<dynamic>(
            DbCollections.Categories,
            Builders<dynamic>.IndexKeys.Ascending("OwnerUsername"),
            "idx_categories_owner_username");

        await EnsureIndexAsync<dynamic>(
            DbCollections.Categories,
            Builders<dynamic>.IndexKeys.Ascending("IsPublic"),
            "idx_categories_is_public");

        await EnsureIndexAsync<dynamic>(
            DbCollections.Prayers,
            Builders<dynamic>.IndexKeys.Ascending("IsPublic"),
            "idx_prayers_is_public");

        await EnsureIndexAsync<dynamic>(
            DbCollections.Prayers,
            Builders<dynamic>.IndexKeys.Ascending("CreatedAt"),
            "idx_prayers_created_at");

        await EnsureIndexAsync<dynamic>(
            DbCollections.Prayers,
            Builders<dynamic>.IndexKeys.Ascending("AuthorUsername"),
            "idx_prayers_author_username");

        await EnsureIndexAsync<dynamic>(
            DbCollections.Comments,
            Builders<dynamic>.IndexKeys
                .Ascending("ParentId")
                .Ascending("ParentType"),
            "idx_comments_parent");
    }

    private async Task EnsureUniqueIndexAsync<T>(
        string collectionName,
        IndexKeysDefinition<T> keys,
        string indexName,
        bool unique = false)
    {
        var collection = database.GetCollection<T>(collectionName);
        var options = new CreateIndexOptions { Name = indexName, Unique = unique };
        var model = new CreateIndexModel<T>(keys, options);

        try
        {
            await collection.Indexes.CreateOneAsync(model);
            logger.LogInformation("Índice '{IndexName}' em '{Collection}' garantido.", indexName, collectionName);
        }
        catch (MongoCommandException ex) when (ex.CodeName == "IndexOptionsConflict" || ex.CodeName == "IndexKeySpecsConflict")
        {
            // Índice já existe com configuração equivalente — ignorar.
            logger.LogDebug("Índice '{IndexName}' em '{Collection}' já existe.", indexName, collectionName);
        }
    }

    private Task EnsureIndexAsync<T>(
        string collectionName,
        IndexKeysDefinition<T> keys,
        string indexName)
        => EnsureUniqueIndexAsync(collectionName, keys, indexName, unique: false);

    // ─── Seed ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Garante que as mensagens de encorajamento existam no banco, fazendo upsert por Category.
    /// Semanticamente equivalente ao UPSERT AQL do DatabaseInitializer da abordagem multimodel.
    /// </summary>
    private async Task SeedEncouragementMessagesAsync()
    {
        var collection = database.GetCollection<EncouragementMessageModel>(DbCollections.EncouragementMessages);

        var messages = new[]
        {
            new EncouragementMessageModel
            {
                Id = nameof(EncouragementMessageCategoryEnum.Sovereignty),
                Category = EncouragementMessageCategoryEnum.Sovereignty,
                Title = "Soberania e Confiança",
                Message = "Mensagem mockada.",
                ScriptureVerse = "Porque os meus pensamentos não são os vossos pensamentos, nem os vossos caminhos os meus caminhos, diz o Senhor.",
                ScriptureReference = "Isaías 55:8"
            },
            new EncouragementMessageModel
            {
                Id = nameof(EncouragementMessageCategoryEnum.Patience),
                Category = EncouragementMessageCategoryEnum.Patience,
                Title = "O Valor da Espera",
                Message = "Mensagem mockada.",
                ScriptureVerse = "Esperei com paciência no SENHOR, e ele se inclinou para mim, e ouviu o meu clamor.",
                ScriptureReference = "Salmos 40:1"
            },
            new EncouragementMessageModel
            {
                Id = nameof(EncouragementMessageCategoryEnum.Gratitude),
                Category = EncouragementMessageCategoryEnum.Gratitude,
                Title = "Celebre os Feitos do Senhor",
                Message = "Mensagem mockada.",
                ScriptureVerse = "Que darei eu ao Senhor, por todos os benefícios que me tem feito?",
                ScriptureReference = "Salmos 116:12"
            },
            new EncouragementMessageModel
            {
                Id = nameof(EncouragementMessageCategoryEnum.Default),
                Category = EncouragementMessageCategoryEnum.Default,
                Title = "Até Aqui nos Ajudou o Senhor",
                Message = "Mensagem mockada.",
                ScriptureVerse = "Tomou então Samuel uma pedra... e chamou o seu nome Ebenézer, e disse: Até aqui nos ajudou o Senhor.",
                ScriptureReference = "1 Samuel 7:12"
            }
        };

        foreach (var message in messages)
        {
            var filter = Builders<EncouragementMessageModel>.Filter.Eq(m => m.Category, message.Category);
            var options = new ReplaceOptions { IsUpsert = true };
            await collection.ReplaceOneAsync(filter, message, options);
        }

        logger.LogInformation("Mensagens de encorajamento verificadas/inseridas.");
    }
}
