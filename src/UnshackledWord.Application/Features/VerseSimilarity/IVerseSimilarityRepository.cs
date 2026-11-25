using UnshackledWord.Domain.Models.Dbo.VerseSimilarity;

namespace UnshackledWord.Application.Features.VerseSimilarity;

/// <summary>Counts written with a succeeded run (roadmap 0027.2).</summary>
public sealed record VerseSimilarityRunSuccess(int PairCount, int StopWordCount, int DurationMs);

/// <summary>A run row with the display name of the maintainer who started it (null for SeedDb or a deleted user).</summary>
public sealed record VerseSimilarityRunListItem(VerseSimilarityRunDbo Run, string? StartedByName);

/// <summary>Run rows and pairs of the verse similarity (tables <c>VerseSimilarityRuns</c>, <c>VerseSimilarities</c>, migration 0049).</summary>
public interface IVerseSimilarityRepository
{
    /// <summary>
    /// Takes the session-level advisory lock that allows one rebuild at a time. Null when another session holds it; otherwise dispose the
    /// result to release the lock (it holds its own connection).
    /// </summary>
    Task<IAsyncDisposable?> TryAcquireRebuildLockAsync(CancellationToken token = default);

    /// <summary>
    /// Whether any session holds the rebuild lock, read from <c>pg_locks</c> without taking it (a probe that took the lock for a moment could
    /// make a rebuild starting at that moment answer busy).
    /// </summary>
    Task<bool> IsRebuildLockHeldAsync(CancellationToken token = default);

    /// <summary>Marks runs left <c>running</c> by a crashed process as failed. Call only while holding the rebuild lock. Returns the count.</summary>
    Task<int> FailInterruptedRunsAsync(CancellationToken token = default);

    /// <summary>Inserts a <c>running</c> run row with the parameters and returns its id.</summary>
    Task<int> CreateRunAsync(VerseSimilarityParameters parameters, int? startedBy, CancellationToken token = default);

    /// <summary>
    /// One transaction: deletes all pairs, inserts <paramref name="hits"/> under <paramref name="runId"/>, then marks the run <c>succeeded</c>
    /// with the counts <paramref name="complete"/> returns (called after the insert, before the commit, so the duration includes the write).
    /// Readers see either the old or the new pairs.
    /// </summary>
    Task ReplacePairsAsync(int runId, IReadOnlyCollection<VerseSimilarityHit> hits, Func<VerseSimilarityRunSuccess> complete, CancellationToken token = default);

    /// <summary>Marks the run <c>failed</c> with the error and the duration so far; the pairs are not touched.</summary>
    Task MarkFailedAsync(int runId, string error, int durationMs, CancellationToken token = default);

    /// <summary>Stores the evaluation JSON (roadmap 0027.6) on the run row; status and counts stay as they are.</summary>
    Task SetEvaluationAsync(int runId, string evaluationJson, CancellationToken token = default);

    /// <summary>
    /// The raw TSK links (table <c>Tsk</c>, distinct by source and target range) in STEP numbering, for the evaluation (roadmap 0027.6). The
    /// curation layer is ignored: added links do not count, hidden ones still do.
    /// </summary>
    Task<IReadOnlyList<TskLink>> GetTskLinksAsync(CancellationToken token = default);

    /// <summary>The most recent run (any status), or null when there is none.</summary>
    Task<VerseSimilarityRunDbo?> GetLatestRunAsync(CancellationToken token = default);

    /// <summary>The newest <paramref name="count"/> runs (any status), newest first, with the display name of <c>StartedBy</c> (roadmap 0027.5).</summary>
    Task<IReadOnlyList<VerseSimilarityRunListItem>> GetRecentRunsAsync(int count, CancellationToken token = default);

    /// <summary>True when <c>VerseSimilarities</c> has no rows (SeedDb gate).</summary>
    Task<bool> PairsMissingAsync(CancellationToken token = default);

    /// <summary>The run row with this id, or null.</summary>
    Task<VerseSimilarityRunDbo?> GetRunAsync(int runId, CancellationToken token = default);

    /// <summary>
    /// Read side (roadmap 0027.3): the Elberfelder verses <paramref name="fromHebRefId"/>..<paramref name="toHebRefId"/> with their STEP key, and the
    /// stored hits of those verses up to rank <paramref name="limit"/>, best first, with the target resolved to the Elberfelder verse and its text
    /// (<c>TargetHebRefId</c> is null when <c>Elb1871Verses</c> has no verse for the target's <c>LxxRefId</c>).
    /// </summary>
    Task<SimilarVersePage> GetPageAsync(int fromHebRefId, int toHebRefId, int limit, CancellationToken token = default);

    /// <summary>
    /// Roadmap 0027.4: per Elberfelder word of the given verses, the Strong's it renders through the mapping, in the token form of the similarity
    /// (Greek <c>DisambiguatedStrongs</c>, Hebrew/Aramaic the root pieces). An added word (no original of its own) takes the Strong's of its parent word.
    /// Two set-based queries (New and Old Testament) for all verses at once.
    /// </summary>
    Task<IReadOnlyList<ElbWordStrongs>> GetWordStrongsAsync(IReadOnlyCollection<int> hebRefIds, CancellationToken token = default);
}
