using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Application.Repositories;

public interface IWordDetailRepository
{
    Task<ElbWordRow?> GetWordAsync(int wordId, CancellationToken token = default);

    /// <summary>Mapping rows of a German word in the NT, joined to the Greek word and its STEP morphology. Added-word rows have no StepWordId.</summary>
    Task<IReadOnlyList<GreekReadingRow>> GetGreekReadingsAsync(int wordId, CancellationToken token = default);

    /// <summary>Mapping rows of a German word in the OT, joined to the Hebrew/Aramaic word.</summary>
    Task<IReadOnlyList<HebrewReadingRow>> GetHebrewReadingsAsync(int wordId, CancellationToken token = default);

    /// <summary>Other German words that share a mapping with this word: same original word, added word (child) or parent.</summary>
    Task<IReadOnlyList<CoMappedWordRow>> GetCoMappedWordsAsync(int wordId, bool hebrew, CancellationToken token = default);

    /// <summary>Number of words of the testament (Greek NT or Hebrew/Aramaic OT) with this root Strong's number; Hebrew ignores the instance suffix ("H0853_A" counts as "H0853").</summary>
    Task<int> GetOccurrenceCountAsync(string strongs, bool hebrew, CancellationToken token = default);

    /// <summary>The most frequent Elberfelder renderings of this Strong's number (added words excluded), most used first.</summary>
    Task<IReadOnlyList<RenderingRow>> GetRenderingsAsync(string strongs, bool hebrew, int limit, CancellationToken token = default);

    /// <summary>The STEP person or place whose Strong's number equals this one (letter suffix included, compared case-insensitively), or null. Only entries with a brief text.</summary>
    Task<NamedEntityRow?> GetNamedEntityAsync(string strongs, CancellationToken token = default);

    /// <summary>Whether the SBLGNT apparatus has at least one row for this verse (original verse reference id, NT only).</summary>
    Task<bool> HasTextVariantsAsync(int refId, CancellationToken token = default);

    /// <summary>One page of the words with this root Strong's number in canonical order, with the matched Elberfelder word positions and the verse words. Same matching as <see cref="GetOccurrenceCountAsync"/>.</summary>
    Task<IReadOnlyList<StrongsOccurrenceRow>> GetOccurrencesAsync(string strongs, bool hebrew, int skip, int take, CancellationToken token = default);
}
