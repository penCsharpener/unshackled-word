using System.Data;
using System.Text;
using Microsoft.Extensions.Logging;
using UnshackledWord.Application.Abstractions;
using Dapper;
using UnshackledWord.Domain.Extensions;

namespace UnshackledWord.Infrastructure.Services;

public sealed class DbWriter : IDbWriter, IDisposable, IAsyncDisposable
{
    private readonly IDbConnectionFactory _factory;
    private readonly ILogger<DbWriter> _logger;
    private readonly IDbConnection _connection;

    public DbWriter(IDbConnectionFactory factory, ILogger<DbWriter> logger)
    {
        _factory = factory;
        _connection = factory.CreateDbConnection();
        _connection.Open();
        _logger = logger;
    }

    public async Task<int> WriteAsync<T>(string sql, T parameters)
    {
        return await _connection.ExecuteAsync(sql, param: parameters);
    }

    public async Task<int> WriteAsync(string sql, DynamicParameters? parameters)
    {
        return await _connection.ExecuteAsync(sql, param: parameters, commandType: CommandType.Text);
    }

    public async Task<int> WriteAsync(string sql)
    {
        return await _connection.ExecuteAsync(sql);
    }

    public void Dispose()
    {
        _factory.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _factory.DisposeAsync();

        if (_connection is IAsyncDisposable dbConnectionAsyncDisposable)
        {
            await dbConnectionAsyncDisposable.DisposeAsync();
        }
        else
        {
            _connection.Close();
            _connection.Dispose();
        }
    }
}

public interface IDbBulkWriter<T>
{
    Task BulkInsertAsync(string tableName, string[] columnNames, int bulkSize, IList<T> items);
}

public sealed class DbBulkWriter<T> : IDbBulkWriter<T>
{
    private readonly IDbConnectionFactory _factory;
    private readonly ILogger<DbBulkWriter<T>> _logger;

    public DbBulkWriter(IDbConnectionFactory factory, ILogger<DbBulkWriter<T>> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task BulkInsertAsync(string tableName, string[] columnNames, int bulkSize, IList<T> items)
    {
        var bulkList = new List<T>();
        var counter = 1;
        var sql = CreateCommand(tableName, columnNames, bulkSize);

        foreach (var item in items)
        {
            if (counter == bulkSize)
            {
            }

            bulkList.Add(item);
            counter++;
        }
    }

    private async Task InternalInsertAsync(string tableName, string[] columnNames, IList<T> items)
    {
    }

    private string CreateCommand(string tableName, string[] columnNames, int bulkSize)
    {
        var sql = $"""
                   INSERT INTO {tableName} ({columnNames.JoinStrings(", ")})
                   VALUES
                   """;

        var sb = new StringBuilder();
        for (int i = 1; i <= bulkSize; i++)
        {
            sb.AppendLine($"({columnNames.Select(x => "@" + x + i).JoinStrings(", ")})");
        }

        return "";
    }
}
