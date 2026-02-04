using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

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
        typeof(UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser.ElberfelderTextRunner),
        typeof(UnshackledWord.Tooling.SeedDb.Services.GlobalBibleTools.GbtRunner),
        typeof(UnshackledWord.Tooling.SeedDb.Services.Tsk.TskRunner),
// #if DEBUG
         typeof(UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser.ElbRunner),
        typeof(UnshackledWord.Tooling.SeedDb.Services.StatisticalRestorationGnt.SrRunner),
//         typeof(UnshackledWord.Tooling.SeedDb.Services.CsvImports.CsvRunner),
        typeof(UnshackledWord.Tooling.SeedDb.Services.SBL.SblRunner),
        typeof(UnshackledWord.Tooling.SeedDb.Services.ByzTxt.ByzRunner),
        // typeof(UnshackledWord.Tooling.SeedDb.Services.Elb1871WordsSrGntWordsMapper.Elb1871SrMappingRunner),
        typeof(UnshackledWord.Tooling.SeedDb.Services.StepBible.StepRunner),
//         typeof(UnshackledWord.Tooling.SeedDb.Services.Elb1871Lemmatizer.LemmatizerRunner)
//         typeof(UnshackledWord.Tooling.SeedDb.Services.BibelKommentare.BkRunner),
//         typeof(UnshackledWord.Tooling.SeedDb.Services.OpenScriptureData.OpenScriptureRunner),
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
