using ArangoDBNetStandard;
using ArangoDBNetStandard.CollectionApi.Models;
using ArangoDBNetStandard.DatabaseApi.Models;
using ArangoDBNetStandard.IndexApi.Models;
using ArangoDBNetStandard.Transport.Http;

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
            EnsureCollectionAsync("Users", CollectionType.Document, existingCollections),
            EnsureCollectionAsync("Prayers", CollectionType.Document, existingCollections),
            EnsureCollectionAsync("Comments", CollectionType.Document, existingCollections),
            EnsureCollectionAsync("Categories", CollectionType.Document, existingCollections),
        };

        await Task.WhenAll(tasks);
    }

    private async Task CreateEdges(List<string> existingCollections)
    {
        var tasks = new List<Task>
        {
            EnsureCollectionAsync("Friendships", CollectionType.Edge, existingCollections),
            EnsureCollectionAsync("PostedBy", CollectionType.Edge, existingCollections),
            EnsureCollectionAsync("CreatedCategory", CollectionType.Edge, existingCollections),
            EnsureCollectionAsync("CategorizedAs", CollectionType.Edge, existingCollections),
            EnsureCollectionAsync("InteractsWith", CollectionType.Edge, existingCollections)
        };

        await Task.WhenAll(tasks);
    }

    private async Task CreateIndexes()
    {
        await EnsureUniqueIndexAsync("Users", ["Email"], "idx_unique_email");
        await EnsureUniqueIndexAsync("Users", ["UserName"], "idx_unique_username");
        await EnsurePersistentIndexAsync("Categories", ["OwnerUsername"], "idx_categories_owner_username");
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
}
