using Neo4j.Driver;

namespace EbenezerBackend.Infrastructure.Data;

// ReSharper disable once TypeParameterCanBeVariant
public interface INeo4JModel<TModel> where TModel : INeo4JModel<TModel>
{
    static abstract TModel FromRecord(IRecord record);
}
