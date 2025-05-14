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
    public IDbConnection CreateDbConnection()
    {
        return new SqliteConnection(_connectionString);
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
