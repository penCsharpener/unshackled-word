using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services;

public sealed partial class SeedingService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SeedingService> _logger;

    private Type[] _runnerTypes =
    [
        typeof(SttsSeed.SttsSeedRunner),
        typeof(RevisionSeed.RevisionLevelSeedRunner),
        typeof(ElberfelderParser.ElberfelderTextRunner),
        typeof(ElberfelderParser.Elb1871VerseRunner),
        typeof(RepoSeed.Morphology.MorphologySeedRunner),
        typeof(Tsk.TskRunner),
        typeof(Tsk.TskCurationSeedRunner),
        typeof(StatisticalRestorationGnt.SrRunner),
        typeof(SBL.SblRunner),
        typeof(StepBible.StepDataBibleTextImporter),
        typeof(StepBible.StepDataStrongsImporter),
        typeof(StepBible.StepDataMorphologyImporter),
        typeof(StepBible.StepDataLexiconImporter),
        typeof(StepBible.StepDataRelationshipImporter),
        // after the Hebrew normalization above: the Old Testament tokens come from StepHebrewWordsNormalized (roadmap 0027.2)
        typeof(VerseSimilarity.VerseSimilarityRunner),
        // after the STEP download, which holds TVTMS (roadmap 0028.3); only while LxxVerses is empty
        typeof(Swete.LxxTextImporter),
        // the confirmed LXX tags and links of the repo seed, only while LxxWordTags has no confirmed row (roadmap 0028.8)
        typeof(Swete.LxxTagSeedRunner),
        typeof(StepBible.StrongsToText.StepStrongsImport),
        typeof(AiMappingImport.AiMappingImportRunner),
        typeof(RevisionSeed.RevisionSeedRunner),
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
