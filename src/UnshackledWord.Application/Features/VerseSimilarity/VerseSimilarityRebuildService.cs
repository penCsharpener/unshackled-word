using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace UnshackledWord.Application.Features.VerseSimilarity;

public enum VerseSimilarityRebuildStatus
{
    Succeeded,
    Failed,

    /// <summary>Another rebuild holds the advisory lock; nothing was written (the maintainer endpoint answers 409).</summary>
    Busy,

    /// <summary>The parameters do not fit the run row; nothing was written.</summary>
    Invalid,
}

/// <param name="RunId">Null for <see cref="VerseSimilarityRebuildStatus.Busy"/> and <see cref="VerseSimilarityRebuildStatus.Invalid"/>.</param>
public sealed record VerseSimilarityRebuildResult(
    VerseSimilarityRebuildStatus Status,
    int? RunId = null,
    int PairCount = 0,
    int StopWordCount = 0,
    int DurationMs = 0,
    string? Error = null,
    VerseSimilarityEvaluation? Evaluation = null);

/// <summary>
/// Rebuilds <c>VerseSimilarities</c> (roadmap 0027.2, 04-model.md "Rebuild flow"): advisory lock, run row <c>running</c>, tokens of both
/// testaments, scoring, atomic replace of all pairs plus run row <c>succeeded</c>. On an exception the run row becomes <c>failed</c> with the
/// error and the old pairs stay. After a succeeded replace the run is evaluated against TSK and the golden pairs (roadmap 0027.6, stored in
/// <c>Evaluation</c>; a failing evaluation never fails the run). Used by the SeedDb verb and pipeline and the maintainer job (0027.5).
/// </summary>
public sealed class VerseSimilarityRebuildService
{
    private readonly IVerseTokenRepository _tokens;
    private readonly IVerseSimilarityRepository _repository;
    private readonly IOptions<VerseSimilarityOptions> _options;
    private readonly ILogger<VerseSimilarityRebuildService> _logger;

    public VerseSimilarityRebuildService(IVerseTokenRepository tokens, IVerseSimilarityRepository repository, IOptions<VerseSimilarityOptions> options,
        ILogger<VerseSimilarityRebuildService> logger)
    {
        _tokens = tokens;
        _repository = repository;
        _options = options;
        _logger = logger;
    }

    /// <summary>The configured defaults (section <c>VerseSimilarity</c>).</summary>
    public VerseSimilarityParameters DefaultParameters => _options.Value.ToParameters();

    /// <summary>Null when the parameters fit the run row columns (<c>numeric(6,4)</c>, <c>int2</c>), otherwise the reason.</summary>
    public static string? Validate(VerseSimilarityParameters parameters) => parameters switch
    {
        { StopDfShare: <= 0 or > 1 } => "StopDfShare must be greater than 0 and at most 1",
        { TopK: < 1 or > 1000 } => "TopK must be between 1 and 1000",
        { MinScore: < 0 or >= 100 } => "MinScore must be at least 0 and below 100",
        { PhraseWeight: < 0 or >= 100 } => "PhraseWeight must be at least 0 and below 100",
        _ => null,
    };

    /// <param name="parameters">Null = <see cref="DefaultParameters"/>.</param>
    /// <param name="startedBy">User id of the maintainer; null = SeedDb.</param>
    public async Task<VerseSimilarityRebuildResult> RebuildAsync(VerseSimilarityParameters? parameters = null, int? startedBy = null, CancellationToken token = default)
    {
        parameters ??= DefaultParameters;
        if (Validate(parameters) is { } invalid)
        {
            return new VerseSimilarityRebuildResult(VerseSimilarityRebuildStatus.Invalid, Error: invalid);
        }

        await using var rebuildLock = await _repository.TryAcquireRebuildLockAsync(token);
        if (rebuildLock is null)
        {
            _logger.LogWarning("Verse similarity rebuild skipped: another rebuild is running");
            return new VerseSimilarityRebuildResult(VerseSimilarityRebuildStatus.Busy);
        }

        var interrupted = await _repository.FailInterruptedRunsAsync(token);
        if (interrupted > 0)
        {
            _logger.LogWarning("Verse similarity: marked {Count} interrupted run(s) as failed", interrupted);
        }

        var watch = Stopwatch.StartNew();
        var runId = await _repository.CreateRunAsync(parameters, startedBy, token);
        _logger.LogInformation("Verse similarity run {RunId} started: StopDfShare {StopDfShare}, TopK {TopK}, MinScore {MinScore}, PhraseWeight {PhraseWeight}",
            runId, parameters.StopDfShare, parameters.TopK, parameters.MinScore, parameters.PhraseWeight);

        try
        {
            var hits = new List<VerseSimilarityHit>();
            var stopWordCount = 0;

            foreach (var testament in new[] { Testament.Old, Testament.New })
            {
                var verses = await _tokens.LoadAsync(testament, token);
                var corpus = VerseSimilarityScorer.BuildCorpus(verses, parameters);
                var testamentHits = VerseSimilarityScorer.ScoreAll(corpus, token);
                var stops = corpus.StopList().Count;

                hits.AddRange(testamentHits);
                stopWordCount += stops;
                _logger.LogInformation("Verse similarity run {RunId}, {Testament}: {Verses} verses, {Stops} stop words, {Pairs} pairs ({Elapsed:F1} s so far)",
                    runId, testament, corpus.VerseCount, stops, testamentHits.Count, watch.Elapsed.TotalSeconds);
            }

            VerseSimilarityRunSuccess? success = null;
            await _repository.ReplacePairsAsync(runId, hits,
                () => success = new VerseSimilarityRunSuccess(hits.Count, stopWordCount, (int)watch.ElapsedMilliseconds), token);

            _logger.LogInformation("Verse similarity run {RunId} succeeded: {Pairs} pairs, {Stops} stop words, {Duration:F1} s",
                runId, success!.PairCount, success.StopWordCount, success.DurationMs / 1000.0);

            var evaluation = await StoreEvaluationAsync(runId, hits, parameters.TopK, token);
            return new VerseSimilarityRebuildResult(VerseSimilarityRebuildStatus.Succeeded, runId, success.PairCount, success.StopWordCount, success.DurationMs,
                Evaluation: evaluation);
        }
        catch (Exception e)
        {
            var durationMs = (int)watch.ElapsedMilliseconds;
            var error = e is OperationCanceledException ? "cancelled" : $"{e.GetType().Name}: {e.Message}";
            _logger.LogError(e, "Verse similarity run {RunId} failed after {Duration:F1} s; the previous pairs stay", runId, durationMs / 1000.0);

            try
            {
                // Not the caller's token: a cancelled run must still be recorded as failed.
                await _repository.MarkFailedAsync(runId, error, durationMs, CancellationToken.None);
            }
            catch (Exception markError)
            {
                _logger.LogError(markError, "Verse similarity run {RunId}: could not mark the run as failed", runId);
            }

            return new VerseSimilarityRebuildResult(VerseSimilarityRebuildStatus.Failed, runId, DurationMs: durationMs, Error: error);
        }
    }

    /// <summary>
    /// Roadmap 0027.6: recall of the raw TSK links and the golden pair ranks for <paramref name="hits"/>. Never throws: a failure (also a
    /// cancellation) becomes <see cref="VerseSimilarityEvaluation.Error"/>. Used after a rebuild and by the dry run of the SeedDb verb.
    /// </summary>
    public async Task<VerseSimilarityEvaluation> EvaluateAsync(IReadOnlyCollection<VerseSimilarityHit> hits, int topK, CancellationToken token = default)
    {
        try
        {
            var links = await _repository.GetTskLinksAsync(token);
            return VerseSimilarityEvaluator.Evaluate(hits, links, topK);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Verse similarity evaluation failed");
            return VerseSimilarityEvaluation.Failed(topK, e is OperationCanceledException ? "cancelled" : $"{e.GetType().Name}: {e.Message}");
        }
    }

    /// <summary>Evaluates and stores the JSON on the (already succeeded) run row; a failure here never fails the run.</summary>
    private async Task<VerseSimilarityEvaluation> StoreEvaluationAsync(int runId, IReadOnlyCollection<VerseSimilarityHit> hits, int topK, CancellationToken token)
    {
        var evaluation = await EvaluateAsync(hits, topK, token);
        try
        {
            // Not the caller's token: the pairs are written, the evaluation belongs to them.
            await _repository.SetEvaluationAsync(runId, evaluation.ToJson(), CancellationToken.None);
            _logger.LogInformation("Verse similarity run {RunId} evaluation:\n{Evaluation}", runId, evaluation.ToSummary());
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Verse similarity run {RunId}: could not store the evaluation", runId);
        }

        return evaluation;
    }
}
