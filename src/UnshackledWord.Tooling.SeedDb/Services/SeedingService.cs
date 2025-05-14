using UnshackledWord.Tooling.SeedDb.Services.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;
using UnshackledWord.Tooling.SeedDb.Services.StatisticalRestorationGnt;

namespace UnshackledWord.Tooling.SeedDb.Services;

public sealed class SeedingService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Type[] _runnerTypes =
    {
        typeof(ElbRunner),
        typeof(SrRunner),
        // typeof(BkRunner),
    };

    public SeedingService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task SeedDatabaseAsync(CancellationToken token = default)
    {
        using var scope = _scopeFactory.CreateScope();

        foreach (var runnerType in _runnerTypes)
        {
            var runner = (IRunner)scope.ServiceProvider.GetRequiredService(runnerType);
            await runner.Run(token);
        }
    }
}
