using System.Data;

namespace UnshackledWord.Application.Abstractions;

public interface IDbReader
{
    Task<T?> ReadFirstOrDefaultAsync<T>(string sql, object param = null);
}

public interface IDbWriter
{
    Task<int> WriteAsync<T>(string sql, T parameters);
    Task<int> WriteAsync(string sql);
}

public interface IDbMigrator
{
}

public interface IDbConnectionFactory : IDisposable, IAsyncDisposable
{
    public IDbConnection DbConnection { get; set; }

    IDbConnection CreateDbConnection();
}
