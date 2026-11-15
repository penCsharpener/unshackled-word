using Microsoft.Extensions.Logging;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Seed.BaseText;
using UnshackledWord.Infrastructure.Seed.Mappings;
using UnshackledWord.Infrastructure.Seed.Morphology;
using UnshackledWord.Infrastructure.Seed.Revision;

namespace UnshackledWord.Infrastructure.Seed;

/// <summary>
/// The rebase of roadmap 0010.11 / 0011.13 as one service for the <c>rebase</c> verb and the maintainer page:
/// 1. exports the morphology, mapping and revision seed of the current database into the layout,
/// 2. empties words, verses, morphology and mappings and rebuilds words and verses from the base text,
/// 3. imports morphology and mappings again through the anchored importers,
/// 4. recomputes the base links of the revision (its rows are kept).
/// Not transactional as a whole: if it fails after step 2, the exported seed is the way back.
/// </summary>
public sealed class SeedRebaseService
{
    private readonly MorphologyExporter _morphologyExporter;
    private readonly MappingExporter _mappingExporter;
    private readonly RevisionExporter _revisionExporter;
    private readonly RevisionBaseRealigner _revisionRealigner;
    private readonly MorphologySeedImporter _morphologyImporter;
    private readonly MappingSeedImporter _mappingImporter;
    private readonly RebaseRepository _repository;
    private readonly Elberfelder1871Strategy _wordStrategy;
    private readonly Elberfelder1871VerseStrategy _verseStrategy;
    private readonly IFileService _fileService;
    private readonly ILogger<SeedRebaseService> _logger;

    public SeedRebaseService(
        MorphologyExporter morphologyExporter,
        MappingExporter mappingExporter,
        RevisionExporter revisionExporter,
        RevisionBaseRealigner revisionRealigner,
        MorphologySeedImporter morphologyImporter,
        MappingSeedImporter mappingImporter,
        RebaseRepository repository,
        Elberfelder1871Strategy wordStrategy,
        Elberfelder1871VerseStrategy verseStrategy,
        IFileService fileService,
        ILogger<SeedRebaseService> logger)
    {
        _morphologyExporter = morphologyExporter;
        _mappingExporter = mappingExporter;
        _revisionExporter = revisionExporter;
        _revisionRealigner = revisionRealigner;
        _morphologyImporter = morphologyImporter;
        _mappingImporter = mappingImporter;
        _repository = repository;
        _wordStrategy = wordStrategy;
        _verseStrategy = verseStrategy;
        _fileService = fileService;
        _logger = logger;
    }

    public async Task<SeedRebaseResult> RebaseAsync(SeedLayout layout, bool confirmBackup, IProgress<string>? progress, CancellationToken token)
    {
        var result = new SeedRebaseResult();

        if (!confirmBackup)
        {
            _logger.LogError("rebase empties the German words, morphology and mappings and imports them again. Make a database backup first and confirm it. Nothing was changed.");
            result.Refused = RebaseRefusal.BackupNotConfirmed;

            return result;
        }

        if (!_fileService.FileExists(layout.BaseTextPath))
        {
            _logger.LogError("Base text {Path} not found. Nothing was changed.", layout.BaseTextPath);
            result.Refused = RebaseRefusal.BaseTextMissing;

            return result;
        }

        if (await _repository.MappingV2HoldsRowsAsync())
        {
            _logger.LogError("Mapping v2 holds rows. A rebase would run into its foreign keys and append-only guards; use the source word sync of roadmap 0005 instead. Nothing was changed.");
            result.Refused = RebaseRefusal.MappingV2;

            return result;
        }

        var before = await _repository.ReadCountsAsync();
        result.Before = ToCounts(before);

        if (before.Words == 0 || before.Morphology == 0)
        {
            _logger.LogError("The database has {Words} words and {Morphology} morphology rows; exporting that would overwrite the seed with an empty one. Nothing was changed.", before.Words, before.Morphology);
            result.Refused = RebaseRefusal.EmptyDatabase;

            return result;
        }

        Report(progress, $"Rebase 1/3: exporting the seed ({before.Words} words, {before.Morphology} morphology rows, {before.Mappings} mapping rows) to {layout.SeedRoot}");
        await _morphologyExporter.ExportAsync(layout, token);
        await _mappingExporter.ExportAsync(layout, token);
        await _revisionExporter.ExportAsync(layout, token);

        Report(progress, $"Rebase 2/3: rebuilding German words and verses from {layout.BaseTextPath}");
        await _repository.ClearAsync(token);
        await _wordStrategy.SaveToDatabase(layout.BaseTextPath, token);
        await _verseStrategy.SaveToDatabase(layout.BaseTextPath, token);

        Report(progress, "Rebase 3/3: importing morphology and mappings through the anchored importers");
        result.Steps.Add(await _morphologyImporter.RunAsync(layout, token));
        Report(progress, "Morphology imported, importing mappings");
        result.Steps.Add(await ImportMappingsAsync(layout, token));

        // The revision keeps its rows (text, mapping, history); only the links of its words to the rebuilt base words are derived again.
        var (realigned, missing) = await _revisionRealigner.RunAsync(token);
        result.RevisionRealigned = realigned;
        result.RevisionMissing = missing;
        Report(progress, $"Revision: base links of {realigned} active variants recomputed, {missing} variants sit on verses that no longer exist");

        var after = await _repository.ReadCountsAsync();
        result.After = ToCounts(after);
        Report(progress, $"Rebase done: words {before.Words} -> {after.Words}, morphology {before.Morphology} -> {after.Morphology}, mapping rows {before.Mappings} -> {after.Mappings}. Refresh the data-quality snapshot.");

        return result;
    }

    private static SeedRebaseCounts ToCounts(RebaseCounts counts) => new() { Words = counts.Words, Morphology = counts.Morphology, Mappings = counts.Mappings };

    private async Task<SeedStepReport> ImportMappingsAsync(SeedLayout layout, CancellationToken token)
    {
        var source = await _mappingImporter.LoadSourceAsync(layout, token);

        if (source is null)
        {
            _logger.LogWarning("No mapping seed in {Folder}; mappings were not imported", layout.MappingsRoot);

            return SeedStepReport.Skip(MappingSeedImporter.Kind, "import", SeedSkipReason.NoSeed);
        }

        return await _mappingImporter.ImportAsync(layout, source, token);
    }

    private void Report(IProgress<string>? progress, string message)
    {
        _logger.LogInformation("{Message}", message);
        progress?.Report(message);
    }
}
