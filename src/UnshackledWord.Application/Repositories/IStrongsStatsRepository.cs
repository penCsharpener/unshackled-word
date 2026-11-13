using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Application.Repositories;

/// <summary>
/// Strong's number statistics, German word statistics and the filtered concordance.
/// Keys are the match keys of <see cref="Features.StrongsStats.StrongsMatchKeys"/> (e.g. "G3004G", "H1254A"); hebrew selects the Hebrew/Aramaic tables.
/// </summary>
public interface IStrongsStatsRepository
{
    /// <summary>Occurrences and verses per book (Elberfelder book numbering), ascending.</summary>
    Task<IReadOnlyList<StrongsBookCountRow>> GetBookCountsAsync(IReadOnlyList<string> keys, bool hebrew, CancellationToken token = default);

    /// <summary>Occurrences per key.</summary>
    Task<IReadOnlyList<StrongsKeyCountRow>> GetKeyCountsAsync(IReadOnlyList<string> keys, bool hebrew, CancellationToken token = default);

    /// <summary>All Elberfelder renderings (added words excluded) per German word key, most used first.</summary>
    Task<IReadOnlyList<StrongsRenderingRow>> GetRenderingsAsync(IReadOnlyList<string> keys, bool hebrew, CancellationToken token = default);

    /// <summary>Occurrences with at least one non-added Elberfelder word.</summary>
    Task<int> GetMappedCountAsync(IReadOnlyList<string> keys, bool hebrew, CancellationToken token = default);

    /// <summary>Per-book statistics of a German word (lower-cased key) within a book range.</summary>
    Task<IReadOnlyList<WordBookCountRow>> GetWordBookCountsAsync(string word, int firstBook, int lastBook, CancellationToken token = default);

    /// <summary>The Strong's numbers (Greek and Hebrew) a German word renders within a book range, most used first.</summary>
    Task<IReadOnlyList<WordOriginalRow>> GetWordOriginalsAsync(string word, int firstBook, int lastBook, CancellationToken token = default);

    /// <summary>Number of concordance hits for the keys, respecting the filter.</summary>
    Task<int> CountOccurrencesAsync(IReadOnlyList<string> keys, bool hebrew, StrongsOccurrenceFilter filter, CancellationToken token = default);

    /// <summary>One page of the concordance in canonical order, with matched Elberfelder positions and the verse words.</summary>
    Task<IReadOnlyList<StrongsOccurrenceRow>> GetOccurrencesAsync(IReadOnlyList<string> keys, bool hebrew, StrongsOccurrenceFilter filter, int skip, int take, CancellationToken token = default);

    // ---- Snapshot readers (Stats* materialized views, roadmap 0008). Only meaningful when GetSnapshotRefreshedAtAsync is not null. ----

    /// <summary>When the stats views were last refreshed successfully; null before the first refresh (readers then use the live SQL).</summary>
    Task<DateTime?> GetSnapshotRefreshedAtAsync(CancellationToken token = default);

    /// <summary>Per match key: occurrences, mapped count and per-book counts of a Strong's number.</summary>
    Task<IReadOnlyList<SnapshotStrongsRow>> GetSnapshotStrongsAsync(IReadOnlyList<string> keys, bool hebrew, CancellationToken token = default);

    /// <summary>German lemma + PoS renderings of the keys (counts summed over the keys), most used first. Added words are never renderings.</summary>
    Task<IReadOnlyList<SnapshotRenderingRow>> GetSnapshotRenderingsAsync(IReadOnlyList<string> keys, bool hebrew, CancellationToken token = default);

    /// <summary>Hebrew-table words with Aramaic grammar for the keys (not in the snapshot; live, one Strong's number).</summary>
    Task<int> GetAramaicCountAsync(IReadOnlyList<string> keys, CancellationToken token = default);

    /// <summary>Resolves a lower-cased German token to the lemma + PoS rows its form maps to (StatsGermanForms); only when it is no known form, the lemma rows with that key. Most frequent first.</summary>
    Task<IReadOnlyList<SnapshotLemmaRow>> ResolveSnapshotLemmasAsync(string word, CancellationToken token = default);

    /// <summary>The originals behind the given lemma + PoS pairs, summed per Strong's key; lang "G"/"H" restricts to one testament, null takes both.</summary>
    Task<IReadOnlyList<WordOriginalRow>> GetSnapshotWordOriginalsAsync(IReadOnlyList<SnapshotLemmaKey> lemmas, string? lang, CancellationToken token = default);

    /// <summary>The top originals per lemma + PoS pair (count descending), with lexicon data.</summary>
    Task<IReadOnlyList<SnapshotLemmaOriginalRow>> GetSnapshotLemmaOriginalsAsync(IReadOnlyList<SnapshotLemmaKey> lemmas, int top, CancellationToken token = default);
}
