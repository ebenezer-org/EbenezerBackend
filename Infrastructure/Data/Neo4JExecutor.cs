using Neo4j.Driver;

namespace EbenezerBackend.Infrastructure.Data;

public interface INeo4JExecutor
{
    Task<IReadOnlyList<TResult>> ExecuteReadListAsync<TResult>(
        string cypher,
        object? parameters,
        Func<IRecord, TResult> map,
        CancellationToken ct);

    Task<TResult> ExecuteReadSingleAsync<TResult>(
        string cypher,
        object? parameters,
        Func<IRecord, TResult> map,
        CancellationToken ct);

    Task<TResult> ExecuteWriteSingleAsync<TResult>(
        string cypher,
        object? parameters,
        Func<IRecord, TResult> map,
        CancellationToken ct);

    Task<TResult?> ExecuteWriteSingleOrDefaultAsync<TResult>(
        string cypher,
        object? parameters,
        Func<IRecord, TResult> map,
        CancellationToken ct) where TResult : class;

    Task<TResult> ExecuteReadTransactionAsync<TResult>(
        Func<IAsyncQueryRunner, CancellationToken, Task<TResult>> work,
        CancellationToken ct);

    Task<TResult> ExecuteWriteTransactionAsync<TResult>(
        Func<IAsyncQueryRunner, CancellationToken, Task<TResult>> work,
        CancellationToken ct);
}

public sealed class Neo4JExecutor(IDriver driver, Neo4JSettings settings) : INeo4JExecutor
{
    private QueryConfig ReadConfig() =>
        new(database: settings.Database, routing: RoutingControl.Readers);

    private QueryConfig WriteConfig() =>
        new(database: settings.Database, routing: RoutingControl.Writers);

    public async Task<IReadOnlyList<TResult>> ExecuteReadListAsync<TResult>(
        string cypher, object? parameters, Func<IRecord, TResult> map, CancellationToken ct)
    {
        var result = await ExecuteAsync(cypher, parameters, ReadConfig(), ct);
        return MapAll(result.Result, map);
    }

    public async Task<TResult> ExecuteReadSingleAsync<TResult>(
        string cypher, object? parameters, Func<IRecord, TResult> map, CancellationToken ct)
    {
        var result = await ExecuteAsync(cypher, parameters, ReadConfig(), ct);
        return MapSingle(result.Result, map);
    }

    public async Task<TResult> ExecuteWriteSingleAsync<TResult>(
        string cypher, object? parameters, Func<IRecord, TResult> map, CancellationToken ct)
    {
        var result = await ExecuteAsync(cypher, parameters, WriteConfig(), ct);
        return MapSingle(result.Result, map);
    }

    public async Task<TResult?> ExecuteWriteSingleOrDefaultAsync<TResult>(
        string cypher, object? parameters, Func<IRecord, TResult> map, CancellationToken ct)
        where TResult : class
    {
        var result = await ExecuteAsync(cypher, parameters, WriteConfig(), ct);
        var records = result.Result;
        return records.Count == 0 ? null : Map(records[0], map);
    }

    public async Task<TResult> ExecuteReadTransactionAsync<TResult>(
        Func<IAsyncQueryRunner, CancellationToken, Task<TResult>> work, CancellationToken ct)
    {
        await using var session = driver.AsyncSession(cfg => cfg.WithDatabase(settings.Database));
        return await session.ExecuteReadAsync(runner => work(runner, ct));
    }

    public async Task<TResult> ExecuteWriteTransactionAsync<TResult>(
        Func<IAsyncQueryRunner, CancellationToken, Task<TResult>> work, CancellationToken ct)
    {
        await using var session = driver.AsyncSession(cfg => cfg.WithDatabase(settings.Database));
        return await session.ExecuteWriteAsync(runner => work(runner, ct));
    }

    private Task<EagerResult<IReadOnlyList<IRecord>>> ExecuteAsync(
        string cypher, object? parameters, QueryConfig config, CancellationToken ct) =>
        driver
            .ExecutableQuery(cypher)
            .WithParameters(parameters ?? new { })
            .WithConfig(config)
            .ExecuteAsync(ct);

    private static IReadOnlyList<TResult> MapAll<TResult>(
        IReadOnlyList<IRecord> records, Func<IRecord, TResult> map)
    {
        var mapped = new List<TResult>(records.Count);
        foreach (var record in records)
            mapped.Add(Map(record, map));
        return mapped;
    }

    private static TResult MapSingle<TResult>(
        IReadOnlyList<IRecord> records, Func<IRecord, TResult> map)
    {
        if (records.Count != 1)
            throw new Neo4JMappingException(
                $"Expected exactly one record from the Neo4j query but received {records.Count}.");

        return Map(records[0], map);
    }

    private static TResult Map<TResult>(IRecord record, Func<IRecord, TResult> map)
    {
        try
        {
            return map(record);
        }
        catch (Exception ex) when (
            ex is KeyNotFoundException
                or InvalidCastException
                or FormatException
                or NullReferenceException
                or ArgumentNullException)
        {
            throw new Neo4JMappingException(
                $"Failed to map a Neo4j record because a required alias was missing or a required value was null: {ex.Message}");
        }
    }
}
