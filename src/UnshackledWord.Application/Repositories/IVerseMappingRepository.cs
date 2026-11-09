using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Application.Repositories;

public interface IVerseMappingRepository
{
    /// <summary>Elberfelder words of the verse in reading order. Empty when the verse does not exist.</summary>
    Task<IReadOnlyList<VerseGermanWordRow>> GetGermanWordsAsync(int bibleBookId, int chapter, int verse, CancellationToken token = default);

    /// <summary>STEP Hebrew (hebrew = true) or Greek words of the verse in reading order.</summary>
    Task<IReadOnlyList<VerseOriginalWordRow>> GetOriginalWordsAsync(bool hebrew, int bibleBookId, int chapter, int verse, CancellationToken token = default);

    /// <summary>All stored mapping rows of the verse's German words, unfiltered.</summary>
    Task<IReadOnlyList<VerseMappingRow>> GetMappingRowsAsync(bool hebrew, int bibleBookId, int chapter, int verse, CancellationToken token = default);

    /// <summary>Deletes all mapping rows of the verse's German words and inserts <paramref name="rows"/>, in one transaction.</summary>
    Task ReplaceMappingAsync(bool hebrew, int bibleBookId, int chapter, int verse, IReadOnlyList<VerseMappingRow> rows, CancellationToken token = default);
}
