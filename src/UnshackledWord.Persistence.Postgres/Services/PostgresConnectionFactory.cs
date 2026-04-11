using System.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Extensions;

namespace UnshackledWord.Persistence.Postgres.Services;

public sealed class PostgresDbConnectionFactory : IDbConnectionFactory
{
    private readonly ILogger<PostgresDbConnectionFactory> _logger;
    private readonly string _connectionString;

    public PostgresDbConnectionFactory(IConfiguration configuration, ILogger<PostgresDbConnectionFactory> logger)
    {
        _logger = logger;
        _connectionString = configuration.GetConnectionString("PostgresConnection")!;
        logger.LogDebug("Connection: {con}", _connectionString);
        DbConnection = new NpgsqlConnection(_connectionString);
    }

    public IDbConnection DbConnection { get; set; }

    public IDbConnection CreateDbConnection()
    {
        return new NpgsqlConnection(_connectionString);
    }

    public async Task<IDbConnection> CreateDbConnectionAsync(CancellationToken token = default)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(token);

        return connection;
    }

    public async Task BulkInsertAsync<T>(string tableName, string[] columns, ICollection<T> dataList, Action<IBinaryImporter, T> mapping, CancellationToken token = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(token);

        // 2. Define the COPY command (specifying columns is recommended)
        var copySql = $"COPY {tableName} (\"{columns.JoinStrings("\",\"")}\") FROM STDIN (FORMAT BINARY)";

        // 3. Begin the binary import process
        await using var writer = new PostgresBinaryImporter(await connection.BeginBinaryImportAsync(copySql, token));
        foreach (var item in dataList)
        {
            await writer.StartRowAsync(token);
            mapping(writer, item);
        }

        // 4. Important: Complete the import to commit the data
        await writer.CompleteAsync(token);
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
