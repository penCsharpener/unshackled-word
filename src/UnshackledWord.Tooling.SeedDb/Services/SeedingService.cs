using UnshackledWord.Tooling.SeedDb.Services.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;
using UnshackledWord.Tooling.SeedDb.Services.GlobalBibleTools;

// #if DEBUG
// using UnshackledWord.Tooling.SeedDb.Services.BibelKommentare;
// using UnshackledWord.Tooling.SeedDb.Services.CsvImports;
// using UnshackledWord.Tooling.SeedDb.Services.OpenScriptureData;
// using UnshackledWord.Tooling.SeedDb.Services.StatisticalRestorationGnt;
// #endif

namespace UnshackledWord.Tooling.SeedDb.Services;

public sealed class SeedingService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SeedingService> _logger;

    private readonly Type[] _runnerTypes =
    {
        typeof(ElberfelderTextRunner),
        typeof(GbtRunner)
// #if DEBUG
//         typeof(ElbRunner),
//         typeof(SrRunner),
//         typeof(CsvRunner),
//         typeof(BkRunner),
//         typeof(OpenScriptureRunner),
// #endif
    };

    public SeedingService(IServiceScopeFactory scopeFactory, ILogger<SeedingService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task SeedDatabaseAsync(CancellationToken token = default)
    {
        using var scope = _scopeFactory.CreateScope();

        foreach (var runnerType in _runnerTypes)
        {
            _logger.LogInformation("Running seeding service: {RunnerType}", runnerType.Name);
            var runner = (IRunner)scope.ServiceProvider.GetRequiredService(runnerType);
            await runner.Run(token);
        }
    }
}
