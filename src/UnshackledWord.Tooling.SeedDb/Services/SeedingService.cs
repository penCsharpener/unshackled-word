using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services;

public sealed partial class SeedingService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SeedingService> _logger;

    private Type[] _runnerTypes =
    [
        typeof(ElberfelderParser.ElberfelderTextRunner),
        typeof(Tsk.TskRunner),
        typeof(StatisticalRestorationGnt.SrRunner),
        typeof(SBL.SblRunner),
        typeof(StepBible.StepDataBibleTextImporter),
        typeof(StepBible.StepDataStrongsImporter),
        typeof(StepBible.StepDataMorphologyImporter),
        typeof(StepBible.StepDataLexiconImporter),
        typeof(StepBible.StepDataRelationshipImporter),
        typeof(StepBible.StrongsToText.StepStrongsImport),
        typeof(AiMappingImport.AiMappingImportRunner),
    ];

    public SeedingService(IServiceScopeFactory scopeFactory, ILogger<SeedingService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task SeedDatabaseAsync(CancellationToken token = default)
    {
        using var scope = _scopeFactory.CreateScope();

        FilterSeedRunnerTypes();
        foreach (var runnerType in _runnerTypes)
        {
            _logger.LogInformation("Running seeding service: {RunnerType}", runnerType.Name);
            var runner = (IRunner)scope.ServiceProvider.GetRequiredService(runnerType);
            await runner.Run(token);
        }
    }

    /// <summary>
    /// method used for local selection of specific types in SeedingService.local.cs
    /// in this method you can override or filter _runnerTypes.
    /// However when you want to run the setup in docker then you need to comment out our devlopment version.
    /// Otherwise it will be compiled and included in the docker container.
    /// </summary>
    partial void FilterSeedRunnerTypes();
}
