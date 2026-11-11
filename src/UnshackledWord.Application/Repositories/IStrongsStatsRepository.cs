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
}
