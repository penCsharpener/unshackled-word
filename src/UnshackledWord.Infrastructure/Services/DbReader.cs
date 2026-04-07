using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using UnshackledWord.Application.Abstractions;

namespace UnshackledWord.Infrastructure.Services;

public sealed class DbReader : IDbReader
{
    private readonly IDbConnectionFactory _factory;
    private readonly ILogger<DbReader> _logger;

    public DbReader(IDbConnectionFactory factory, ILogger<DbReader> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<T?> ReadFirstOrDefaultAsync<T>(string sql, object? param = null)
    {
        using var connection = _factory.CreateDbConnection();

        return await connection.QueryFirstOrDefaultAsync<T>(sql, param: param, commandType: CommandType.Text);
    }

    public async Task<IEnumerable<T>> ReadAsListAsync<T>(string sql, object? param = null)
    {
        using var connection = _factory.CreateDbConnection();

        return await connection.QueryAsync<T>(sql, param: param, commandType: CommandType.Text);
    }

    public async Task<List<T>> ReadMultipleAsListAsync<T>(string sql, object? param, Func<IMultiDbReader, Task<List<T>>> mappingFunc, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        var list =  new List<T>();

        await using var multi = await connection.QueryMultipleAsync(sql, param, commandType: CommandType.Text);
        var multiDbReader = new MultiDbReader(multi);

        list.AddRange(await mappingFunc(multiDbReader));

        return list;
    }

    public async Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null)
    {
        if (sql.Contains("insert into", StringComparison.OrdinalIgnoreCase) ||
            sql.Contains("update", StringComparison.OrdinalIgnoreCase) ||
            sql.Contains("delete", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("Do not use IDbReader's ExecuteScalarAsync for insert, update, or delete operations.");
        }

        using var connection = _factory.CreateDbConnection();

        return await connection.ExecuteScalarAsync<T>(sql, param: param, commandType: CommandType.Text);
    }
}

