namespace UnshackledWord.Application.Abstractions;

public interface IDbReader
{
    Task<T?> ReadFirstOrDefaultAsync<T>(string sql, object? param = null);
    Task<IEnumerable<T>> ReadAsListAsync<T>(string sql, object? param = null);
    Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null);

    Task<List<T>> ReadMultipleAsListAsync<T>(string sql, object? param,
        Func<IMultiDbReader, Task<List<T>>> mappingFunc, CancellationToken token = default);
}
