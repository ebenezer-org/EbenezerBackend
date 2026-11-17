using MongoDB.Driver;

namespace EbenezerBackend.Infrastructure.Data;

/// <summary>
/// Traduz códigos de erro do MongoDB para condições nomeadas.
/// Referência: https://www.mongodb.com/docs/manual/reference/error-codes/
/// </summary>
public static class MongoDbErrorTranslator
{
    public const int DuplicateKey = 11000;

    public static bool IsDuplicateKey(MongoWriteException ex)
        => ex.WriteError.Category == ServerErrorCategory.DuplicateKey
           && ex.WriteError.Code == DuplicateKey;
}
