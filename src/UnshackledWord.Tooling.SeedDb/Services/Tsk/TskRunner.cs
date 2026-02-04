using UnshackledWord.Application.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.Tsk;

public sealed class TskRunner : IRunner
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDbReader _dbReader;
    private readonly ILogger<TskRunner> _logger;

    public TskRunner(IServiceScopeFactory scopeFactory, IDbReader dbReader, ILogger<TskRunner> logger)
    {
        _scopeFactory = scopeFactory;
        _dbReader = dbReader;
        _logger = logger;
    }

    public async Task Run(CancellationToken token = default)
    {
        var count = await GetCountAsync(token);

        if (count > 0)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<TskStrategy>();

        await runner.SaveToDatabase("", token);
    }

    private async Task<int> GetCountAsync(CancellationToken token = default)
    {
        var sql = """
                  select COUNT(*)
                  from "unshackled-word"."Tsk"
                  """;

        return await _dbReader.ExecuteScalarAsync<int>(sql);
    }
}
