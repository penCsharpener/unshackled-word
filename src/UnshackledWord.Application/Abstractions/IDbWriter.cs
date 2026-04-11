namespace UnshackledWord.Application.Abstractions;

public interface IDbWriter
{
    Task<int> WriteAsync<T>(string sql, T parameters);
    Task<int> WriteAsync(string sql);
    Task BulkInsertAsync<T>(string tableName, string[] columns, ICollection<T> dataList,
        Action<IBinaryImporter, T> mapping, CancellationToken token = default);
    Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null);
}
