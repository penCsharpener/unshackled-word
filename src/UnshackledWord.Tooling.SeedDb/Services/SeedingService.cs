using UnshackledWord.Tooling.SeedDb.Services.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;
#if DEBUG
using UnshackledWord.Tooling.SeedDb.Services.BibelKommentare;
using UnshackledWord.Tooling.SeedDb.Services.CsvImports;
using UnshackledWord.Tooling.SeedDb.Services.OpenScriptureData;
using UnshackledWord.Tooling.SeedDb.Services.StatisticalRestorationGnt;
#endif

namespace UnshackledWord.Tooling.SeedDb.Services;

public sealed class SeedingService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Type[] _runnerTypes =
    {
        typeof(ElberfelderTextRunner),
#if DEBUG
        typeof(ElbRunner),
        typeof(SrRunner),
        typeof(CsvRunner),
        typeof(BkRunner),
        typeof(OpenScriptureRunner),
#endif
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
