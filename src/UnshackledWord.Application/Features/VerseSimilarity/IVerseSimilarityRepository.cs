using UnshackledWord.Domain.Models.Dbo.VerseSimilarity;

namespace UnshackledWord.Application.Features.VerseSimilarity;

/// <summary>Counts written with a succeeded run (roadmap 0027.2).</summary>
public sealed record VerseSimilarityRunSuccess(int PairCount, int StopWordCount, int DurationMs);

/// <summary>Run rows and pairs of the verse similarity (tables <c>VerseSimilarityRuns</c>, <c>VerseSimilarities</c>, migration 0049).</summary>
public interface IVerseSimilarityRepository
{
    /// <summary>
    /// Takes the session-level advisory lock that allows one rebuild at a time. Null when another session holds it; otherwise dispose the
    /// result to release the lock (it holds its own connection).
    /// </summary>
    Task<IAsyncDisposable?> TryAcquireRebuildLockAsync(CancellationToken token = default);

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

    /// <summary>The most recent run (any status), or null when there is none.</summary>
    Task<VerseSimilarityRunDbo?> GetLatestRunAsync(CancellationToken token = default);

    /// <summary>True when <c>VerseSimilarities</c> has no rows (SeedDb gate).</summary>
    Task<bool> PairsMissingAsync(CancellationToken token = default);
}
