using UnshackledWord.Application.Features.VerseSimilarity;
using UnshackledWord.Seeding.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.StepBible;

namespace UnshackledWord.Tooling.SeedDb.Services.VerseSimilarity;

/// <summary>
/// SeedDb pipeline step of roadmap 0027.2: rebuilds <c>VerseSimilarities</c> after the STEP import, gated like the Greek editions refresh in
/// <see cref="StepDataBibleTextImporter.RefreshGreekEditionsIfNeededAsync"/>: only when this run inserted STEP words or the table is empty.
/// It runs after <see cref="StepDataRelationshipImporter"/>, not inside the word import, because the Hebrew tokens are read from
/// <c>StepHebrewWordsNormalized</c>, which that runner fills; on a fresh database an earlier rebuild would score an empty Old Testament and the
/// gate would then never fill it. A failed rebuild is logged and leaves the old pairs; it does not stop the seed.
/// </summary>
public sealed class VerseSimilarityRunner : IRunner
{
    private readonly StepImportResult _stepImport;
    private readonly IVerseSimilarityRepository _repository;
    private readonly VerseSimilarityRebuildService _rebuild;
    private readonly ILogger<VerseSimilarityRunner> _logger;

    public VerseSimilarityRunner(StepImportResult stepImport, IVerseSimilarityRepository repository, VerseSimilarityRebuildService rebuild,
        ILogger<VerseSimilarityRunner> logger)
    {
        _stepImport = stepImport;
        _repository = repository;
        _rebuild = rebuild;
        _logger = logger;
    }

    public Task Run(CancellationToken token = default) => RebuildIfNeededAsync(_stepImport.WordsInserted, token);

    /// <summary>Returns the rebuild result, or null when the table is up to date and nothing ran.</summary>
    public async Task<VerseSimilarityRebuildResult?> RebuildIfNeededAsync(bool stepWordsInserted, CancellationToken token = default)
    {
        if (!stepWordsInserted && !await _repository.PairsMissingAsync(token))
        {
            _logger.LogInformation("verse similarity up to date, no rebuild");
            return null;
        }

        var result = await _rebuild.RebuildAsync(token: token);
        if (result.Status != VerseSimilarityRebuildStatus.Succeeded)
        {
            _logger.LogError("Verse similarity rebuild {Status} (run {RunId}): {Error}", result.Status, result.RunId, result.Error);
        }

        return result;
    }
}
