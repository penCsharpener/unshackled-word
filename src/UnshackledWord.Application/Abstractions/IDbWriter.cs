namespace UnshackledWord.Application.Abstractions;

public interface IDbWriter
{
    Task<int> WriteAsync<T>(string sql, T parameters);
    Task<int> WriteAsync(string sql);
    Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null);
}