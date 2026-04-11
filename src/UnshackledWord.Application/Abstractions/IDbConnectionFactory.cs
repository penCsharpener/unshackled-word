using System.Data;

namespace UnshackledWord.Application.Abstractions;

public interface IDbConnectionFactory : IDisposable, IAsyncDisposable
{
    public IDbConnection DbConnection { get; set; }

    Task BulkInsertAsync<T>(string tableName, string[] columns, ICollection<T> dataList,
        Action<IBinaryImporter, T> mapping, CancellationToken token = default);

    IDbConnection CreateDbConnection();
    Task<IDbConnection> CreateDbConnectionAsync(CancellationToken token = default);
}
