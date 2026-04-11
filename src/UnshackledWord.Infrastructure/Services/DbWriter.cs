using System.Data;
using Microsoft.Extensions.Logging;
using UnshackledWord.Application.Abstractions;
using Dapper;

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

    public async Task BulkInsertAsync<T>(string tableName, string[] columns, ICollection<T> dataList,
        Action<IBinaryImporter, T> mapping, CancellationToken token = default)
    {
        await _factory.BulkInsertAsync(tableName, columns, dataList, mapping, token);
    }

    public async Task<int> WriteAsync(string sql)
    {
        return await _connection.ExecuteAsync(sql);
    }

    public async Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null)
    {
        using var connection = _factory.CreateDbConnection();

        connection.Open();
        return await connection.ExecuteScalarAsync<T>(sql, param: param, commandType: CommandType.Text);
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
