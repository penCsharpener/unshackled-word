using UnshackledWord.Infrastructure.Seed;
using UnshackledWord.Infrastructure.Seed.Morphology;
using UnshackledWord.Infrastructure.Seed.Mappings;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;
using UnshackledWord.Tooling.SeedDb.Services.RepoSeed.Mappings;
using UnshackledWord.Tooling.SeedDb.Services.RepoSeed.Morphology;

namespace UnshackledWord.Tooling.SeedDb.Services.RepoSeed.Verbs;

/// <summary>
/// <c>rebase --confirm-backup [--out DIR]</c>: the maintainer's step after a parser or base-text fix (roadmap 0010.11).
/// 1. exports the morphology and mapping seed of the current database (into <c>assets</c>, or DIR),
/// 2. empties words, verses, morphology and mappings and rebuilds words and verses from the fixed <c>assets</c>,
/// 3. imports morphology and mappings again through the anchored importers, so every lemma and mapping follows its German word,
/// 4. leaves the reports in <c>temp/seed-report</c> (what was shifted, modified or could not be placed).
/// Refuses without <c>--confirm-backup</c> and while Mapping v2 holds rows (there the registry sync is the path). Not transactional as a
/// whole: if it fails after step 2, the exported seed is the way back (run the seed again on the emptied tables).
/// </summary>
public sealed class RebaseVerb : ISeedVerb
{
    private readonly MorphologyExporter _morphologyExporter;
    private readonly MappingExporter _mappingExporter;
    private readonly MorphologySeedRunner _morphologyRunner;
    private readonly MappingSeedImporter _mappingImporter;
    private readonly RebaseRepository _repository;
    private readonly Elberfelder1871Strategy _wordStrategy;
    private readonly Elberfelder1871VerseStrategy _verseStrategy;
    private readonly IFileService _fileService;
    private readonly DatabaseSeedSettings _settings;
    private readonly ILogger<RebaseVerb> _logger;

    public RebaseVerb(
        MorphologyExporter morphologyExporter,
        MappingExporter mappingExporter,
        MorphologySeedRunner morphologyRunner,
        MappingSeedImporter mappingImporter,
        RebaseRepository repository,
        Elberfelder1871Strategy wordStrategy,
        Elberfelder1871VerseStrategy verseStrategy,
        IFileService fileService,
        IOptions<AppSettings> options,
        ILogger<RebaseVerb> logger)
    {
        _morphologyExporter = morphologyExporter;
        _mappingExporter = mappingExporter;
        _morphologyRunner = morphologyRunner;
        _mappingImporter = mappingImporter;
        _repository = repository;
        _wordStrategy = wordStrategy;
        _verseStrategy = verseStrategy;
        _fileService = fileService;
        _settings = options.Value.DatabaseSeeding;
        _logger = logger;
    }

    public string Name => "rebase";

    public async Task<int> RunAsync(VerbArguments arguments, CancellationToken token)
    {
        if (!arguments.Has("--confirm-backup"))
        {
            _logger.LogError("rebase empties the German words, morphology and mappings and imports them again. Make a database backup first, then run again with --confirm-backup. Nothing was changed.");
            return 2;
        }

        var layout = new SeedLayout(_fileService, _settings, arguments.Get("--out"));

        if (!_fileService.FileExists(layout.BaseTextPath))
        {
            _logger.LogError("Base text {Path} not found. Nothing was changed.", layout.BaseTextPath);
            return 2;
        }

        if (await _repository.MappingV2HoldsRowsAsync())
        {
            _logger.LogError("Mapping v2 holds rows. A rebase would run into its foreign keys and append-only guards; use the source word sync of roadmap 0005 instead. Nothing was changed.");
            return 2;
        }

        var before = await _repository.ReadCountsAsync();

        if (before.Words == 0 || before.Morphology == 0)
        {
            _logger.LogError("The database has {Words} words and {Morphology} morphology rows; exporting that would overwrite the seed with an empty one. Nothing was changed.", before.Words, before.Morphology);
            return 2;
        }

        _logger.LogInformation("Rebase 1/3: exporting the seed ({Words} words, {Morphology} morphology rows, {Mappings} mapping rows) to {Path}", before.Words, before.Morphology, before.Mappings, layout.SeedRoot);
        await _morphologyExporter.ExportAsync(layout, token);
        await _mappingExporter.ExportAsync(layout, token);

        _logger.LogInformation("Rebase 2/3: rebuilding German words and verses from {Path}", layout.BaseTextPath);
        await _repository.ClearAsync(token);
        await _wordStrategy.SaveToDatabase(layout.BaseTextPath, token);
        await _verseStrategy.SaveToDatabase(layout.BaseTextPath, token);

        _logger.LogInformation("Rebase 3/3: importing morphology and mappings through the anchored importers");
        await _morphologyRunner.RunAsync(layout, token);
        await ImportMappingsAsync(layout, token);

        var after = await _repository.ReadCountsAsync();
        _logger.LogInformation(
            "Rebase done: words {WordsBefore} -> {WordsAfter}, morphology {MorphBefore} -> {MorphAfter}, mapping rows {MapBefore} -> {MapAfter}. Reports: {Reports}. Refresh the data-quality snapshot (POST api/dataQuality/refresh).",
            before.Words, after.Words, before.Morphology, after.Morphology, before.Mappings, after.Mappings, layout.ReportRoot);

        return 0;
    }

    private async Task ImportMappingsAsync(SeedLayout layout, CancellationToken token)
    {
        var source = await _mappingImporter.LoadSourceAsync(layout, token);

        if (source is null)
        {
            _logger.LogWarning("No mapping seed in {Folder}; mappings were not imported", layout.MappingsRoot);
            return;
        }

        await _mappingImporter.ImportAsync(layout, source, token);
    }
}
