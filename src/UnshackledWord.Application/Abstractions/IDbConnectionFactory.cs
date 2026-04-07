using System.Data;

namespace UnshackledWord.Application.Abstractions;

public interface IDbConnectionFactory : IDisposable, IAsyncDisposable
{
    public IDbConnection DbConnection { get; set; }

    IDbConnection CreateDbConnection();
    Task<IDbConnection> CreateDbConnectionAsync(CancellationToken token = default);
}