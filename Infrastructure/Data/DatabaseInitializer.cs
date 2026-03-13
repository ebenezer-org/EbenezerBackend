using ArangoDBNetStandard.CollectionApi.Models;
using ArangoDBNetStandard.DatabaseApi.Models;

namespace EbenezerBackend.Infrastructure.Data;

public class DatabaseInitializer(
    ArangoDbContext context,
    ArangoDbSettings settings,
    ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync()
    {
        const int maxRetries = 20;
        const int retryDelaySeconds = 5;

        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                logger.LogInformation($"Iniciando verificação do banco de dados. Tentativa {i+1} de {maxRetries}");
                
                context.Connect("_system");
                
                var databases = await context.Client.Database.GetDatabasesAsync();

                if (!databases.Result.Contains(settings.DatabaseName))
                {
                    await context.Client.Database.PostDatabaseAsync(new PostDatabaseBody
                    {
                        Name = settings.DatabaseName
                    });
                    logger.LogInformation($"Banco {settings.DatabaseName} criado com sucesso.");
                }
                
                context.Connect(settings.DatabaseName);

                await UpsertDocumentCollections();
                await UpsertEdgeCollections();

                logger.LogInformation("Banco de dados pronto para uso.");
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

    private async Task UpsertDocumentCollections()
    {
        await EnsureCollectionAsync("Users", CollectionType.Document);
        await EnsureCollectionAsync("Prayers", CollectionType.Document);
        await EnsureCollectionAsync("Comments", CollectionType.Document);
        await EnsureCollectionAsync("Categories", CollectionType.Document);
    }

    private async Task UpsertEdgeCollections()
    {
        await EnsureCollectionAsync("Friendships", CollectionType.Edge);
        await EnsureCollectionAsync("InteractsWith", CollectionType.Edge);
        await EnsureCollectionAsync("PostedBy", CollectionType.Edge);
    }

    private async Task EnsureCollectionAsync(string name, CollectionType type)
    {
        var collections = await context.Client.Collection.GetCollectionsAsync();
        if (collections.Result.All(c => c.Name != name))
        {
            await context.Client.Collection.PostCollectionAsync(new PostCollectionBody
            {
                Name = name,
                Type = type
            });
            logger.LogInformation($"Coleção '{name}' ({type}) criada.");
        }
    }
}