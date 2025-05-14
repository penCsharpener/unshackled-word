using System.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using UnshackledWord.Application.Abstractions;

namespace UnshackledWord.Persistence.Postgres.Services;

public sealed class PostgresDbConnectionFactory : IDbConnectionFactory
{
    private readonly ILogger<PostgresDbConnectionFactory> _logger;
    private readonly string _connectionString;

    public PostgresDbConnectionFactory(IConfiguration configuration, ILogger<PostgresDbConnectionFactory> logger)
    {
        _logger = logger;
        _connectionString = configuration.GetConnectionString("PostgresConnection")!;
        logger.LogInformation("Connection: {con}", _connectionString);
        DbConnection = new NpgsqlConnection(_connectionString);
    }

    public IDbConnection DbConnection { get; set; }

    public IDbConnection CreateDbConnection()
    {
        return new NpgsqlConnection(_connectionString);
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
