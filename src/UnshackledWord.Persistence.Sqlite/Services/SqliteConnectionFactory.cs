using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UnshackledWord.Application.Abstractions;

namespace UnshackledWord.Persistence.Sqlite.Services;

public sealed class SqliteDbConnectionFactory : IDbConnectionFactory
{
    private readonly ILogger<SqliteDbConnectionFactory> _logger;
    private readonly string _connectionString;

    public SqliteDbConnectionFactory(IConfiguration configuration, ILogger<SqliteDbConnectionFactory> logger)
    {
        _logger = logger;
        _connectionString = configuration.GetConnectionString("SqliteConnection")!;
        logger.LogInformation("Connection: {con}", _connectionString);
        DbConnection = new SqliteConnection(_connectionString);
    }

    public IDbConnection DbConnection { get; set; }

    public Task BulkInsertAsync<T>(string tableName, string[] columns, ICollection<T> dataList, Action<IBinaryImporter, T> mapping,
        CancellationToken token = default)
    {
        throw new NotImplementedException();
    }

    public IDbConnection CreateDbConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        return connection;
    }

    public async Task<IDbConnection> CreateDbConnectionAsync(CancellationToken token = default)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(token);

        return connection;
    }

    public void Dispose()
    {
        DbConnection.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (DbConnection is IAsyncDisposable dbConnectionAsyncDisposable)
        {
            await dbConnectionAsyncDisposable.DisposeAsync();
        }
        else
        {
            DbConnection.Dispose();
        }
    }
}
