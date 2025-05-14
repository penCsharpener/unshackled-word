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

    public async Task<T?> ReadFirstOrDefaultAsync<T>(string sql, object param = null)
    {
        using var connection = _factory.CreateDbConnection();

        connection.Open();
        return await connection.QueryFirstOrDefaultAsync<T>(sql, param);
    }
}
