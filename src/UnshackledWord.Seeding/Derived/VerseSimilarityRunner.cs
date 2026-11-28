using UnshackledWord.Application.Features.VerseSimilarity;
using UnshackledWord.Seeding.Abstractions;
using UnshackledWord.Seeding.StepBible;

namespace UnshackledWord.Seeding.Derived;

/// <summary>
/// SeedDb pipeline step of roadmap 0027.2: rebuilds <c>VerseSimilarities</c> after the STEP import, gated like the Greek editions refresh in
/// <see cref="StepDataBibleTextImporter.RefreshGreekEditionsIfNeededAsync"/>: only when this run inserted STEP words or the table is empty.
/// It runs after <see cref="StepDataRelationshipImporter"/>, not inside the word import, because the Hebrew tokens are read from
/// <c>StepHebrewWordsNormalized</c>, which that runner fills; on a fresh database an earlier rebuild would score an empty Old Testament and the
/// gate would then never fill it. A failed rebuild leaves the old pairs and fails the step (roadmap 0031 review: the setup page shows it as failed,
/// the CLI exits non-zero), instead of ending a job "succeeded" with an empty table.
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

    public async Task Run(CancellationToken token = default)
    {
        var result = await RebuildIfNeededAsync(_stepImport.WordsInserted, token);
        if (result is not null && result.Status != VerseSimilarityRebuildStatus.Succeeded)
        {
            throw new InvalidOperationException($"Verse similarity rebuild {result.Status}: {result.Error}");
        }
    }

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
