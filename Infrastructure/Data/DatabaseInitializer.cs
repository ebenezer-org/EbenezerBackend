using ArangoDBNetStandard;
using ArangoDBNetStandard.CollectionApi.Models;
using ArangoDBNetStandard.DatabaseApi.Models;
using ArangoDBNetStandard.IndexApi.Models;
using ArangoDBNetStandard.Transport.Http;
using EbenezerBackend.Features.Retrospective.Domain.Enums;
using EbenezerBackend.Shared.Data;

namespace EbenezerBackend.Infrastructure.Data;

public class DatabaseInitializer(
    IArangoDBClient client,
    ArangoDbSettings settings,
    ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync()
    {
        const int maxRetries = 20;
        const int retryDelaySeconds = 5;
        
        var systemTransport = HttpApiTransport.UsingBasicAuth(
            new Uri($"{settings.Protocol}://{settings.Host}:{settings.Port}"),
            "_system",
            settings.User,
            settings.Password
        );
        
        var systemArangoClient = new ArangoDBClient(systemTransport);

        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                logger.LogInformation($"Iniciando verificação do banco de dados. Tentativa {i+1} de {maxRetries}");
                
                var databases = await systemArangoClient.Database.GetDatabasesAsync();

                if (!databases.Result.Contains(settings.DatabaseName))
                {
                    await systemArangoClient.Database.PostDatabaseAsync(new PostDatabaseBody
                    {
                        Name = settings.DatabaseName
                    });
                    logger.LogInformation($"Banco {settings.DatabaseName} criado com sucesso.");
                }
                
                var collectionsResponse = await client.Collection.GetCollectionsAsync();
                var existingCollections = collectionsResponse.Result.Select(c => c.Name).ToList();
                
                await CreateCollections(existingCollections);
                await CreateEdges(existingCollections);
                await CreateIndexes();

                await SeedEncouragementMessages();

                logger.LogInformation("Banco de dados pronto para uso.");
                break;
            }
            catch (Exception error)
            {
                logger.LogWarning($"Erro ao inicializar o banco: {error.Message}");
                
                if (i == maxRetries - 1)
                {
                    logger.LogCritical("Esgotadas as tentativas de conexão com o banco de dados.");
                    throw;
                }

                await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
            }
        }
    }
    
    private async Task CreateCollections(List<string> existingCollections)
    {
        var tasks = new List<Task>
        {
            EnsureCollectionAsync(ArangoDbCollections.Users, CollectionType.Document, existingCollections),
            EnsureCollectionAsync(ArangoDbCollections.Prayers, CollectionType.Document, existingCollections),
            EnsureCollectionAsync(ArangoDbCollections.Categories, CollectionType.Document, existingCollections),
            EnsureCollectionAsync(ArangoDbCollections.EncouragementMessages, CollectionType.Document, existingCollections),
        };

        await Task.WhenAll(tasks);
    }

    private async Task CreateEdges(List<string> existingCollections)
    {
        var tasks = new List<Task>
        {
            EnsureCollectionAsync(ArangoDbEdges.Friendships, CollectionType.Edge, existingCollections),
            EnsureCollectionAsync(ArangoDbEdges.PostedBy, CollectionType.Edge, existingCollections),
            EnsureCollectionAsync(ArangoDbEdges.CreatedCategory, CollectionType.Edge, existingCollections),
            EnsureCollectionAsync(ArangoDbEdges.CategorizedAs, CollectionType.Edge, existingCollections),
            EnsureCollectionAsync(ArangoDbEdges.ReactedBy, CollectionType.Edge, existingCollections),
            EnsureCollectionAsync(ArangoDbEdges.CommentedBy, CollectionType.Edge, existingCollections),
        };

        await Task.WhenAll(tasks);
    }

    private async Task CreateIndexes()
    {
        await EnsureUniqueIndexAsync(ArangoDbCollections.Users, ["UserName"], "idx_unique_username");
        await EnsurePersistentIndexAsync(ArangoDbCollections.Categories, ["OwnerUsername"], "idx_categories_owner_username");
        await EnsurePersistentIndexAsync(ArangoDbCollections.Categories, ["IsPublic"], "idx_categories_is_public");
        await EnsurePersistentIndexAsync(ArangoDbCollections.Prayers, ["IsPublic"], "idx_prayers_is_public");
        await EnsurePersistentIndexAsync(ArangoDbCollections.Prayers, ["CreatedAt"], "idx_prayers_created_at");
    }

    private async Task EnsureCollectionAsync(string name, CollectionType type, List<string> existing)
    {
        if (!existing.Contains(name))
        {
            await client.Collection.PostCollectionAsync(new PostCollectionBody
            {
                Name = name,
                Type = type
            });
            logger.LogInformation($"Coleção '{name}' criada.");
        }
    }

    private async Task EnsureUniqueIndexAsync(string collectionName, string[] fields, string indexName)
    {
        var getCollectionsQuery = new GetAllCollectionIndexesQuery
        {
            CollectionName = collectionName
        };

        var indexes = await client.Index.GetAllCollectionIndexesAsync(getCollectionsQuery);
        if (indexes.Indexes.All(i => i.Name != indexName))
        {
            await client.Index.PostPersistentIndexAsync(
                new PostIndexQuery
                {
                    CollectionName = collectionName
                },
                new PostPersistentIndexBody
                {
                    Fields = fields,
                    Unique = true,
                    Name = indexName
                });
        }
    }

    private async Task EnsurePersistentIndexAsync(string collectionName, string[] fields, string indexName)
    {
        var getCollectionsQuery = new GetAllCollectionIndexesQuery
        {
            CollectionName = collectionName
        };

        var indexes = await client.Index.GetAllCollectionIndexesAsync(getCollectionsQuery);
        if (indexes.Indexes.All(i => i.Name != indexName))
        {
            await client.Index.PostPersistentIndexAsync(
                new PostIndexQuery
                {
                    CollectionName = collectionName
                },
                new PostPersistentIndexBody
                {
                    Fields = fields,
                    Unique = false,
                    Name = indexName
                });
        }
    }

    private async Task SeedEncouragementMessages()
    {
        var query = $@"
            LET messages = [
                {{
                    _key: ""{nameof(EncouragementMessageCategoryEnum.Sovereignty)}"",
                    Title: ""Soberania e Confiança"",
                    Message: ""Mensagem mockada."",
                    ScriptureVerse: ""Porque os meus pensamentos não são os vossos pensamentos, nem os vossos caminhos os meus caminhos, diz o Senhor."",
                    ScriptureReference: ""Isaías 55:8""
                }},
                {{
                    _key: ""{nameof(EncouragementMessageCategoryEnum.Patience)}"",
                    Title: ""O Valor da Espera"",
                    Message: ""Mensagem mockada."",
                    ScriptureVerse: ""Esperei com paciência no SENHOR, e ele se inclinou para mim, e ouviu o meu clamor."",
                    ScriptureReference: ""Salmos 40:1""
                }},
                {{
                    _key: ""{nameof(EncouragementMessageCategoryEnum.Gratitude)}"",
                    Title: ""Celebre os Feitos do Senhor"",
                    Message: ""Mensagem mockada."",
                    ScriptureVerse: ""Que darei eu ao Senhor, por todos os benefícios que me tem feito?"",
                    ScriptureReference: ""Salmos 116:12""
                }},
                {{
                    _key: ""{nameof(EncouragementMessageCategoryEnum.Default)}"",
                    Title: ""Até Aqui nos Ajudou o Senhor"",
                    Message: ""Mensagem mockada."",
                    ScriptureVerse: ""Tomou então Samuel uma pedra... e chamou o seu nome Ebenézer, e disse: Até aqui nos ajudou o Senhor."",
                    ScriptureReference: ""1 Samuel 7:12""
                }}
            ]

            FOR message IN messages
                UPSERT {{ _key: message._key }}
                INSERT message
                UPDATE message
                IN EncouragementMessages
        ";

        await client.Cursor.PostCursorAsync(query);
    }
}
