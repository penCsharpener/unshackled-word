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

    /// <summary>STEP Hebrew or Greek words of all verses of the chapter (HebRefId ordering). Read only, for the side panel.</summary>
    Task<IReadOnlyList<ChapterOriginalWordRow>> GetChapterOriginalWordsAsync(bool hebrew, int bibleBookId, int chapter, CancellationToken token = default);

    /// <summary>Stored non-added links of the chapter's German words, with the verse of the German word.</summary>
    Task<IReadOnlyList<ChapterMappingRow>> GetChapterMappingRowsAsync(bool hebrew, int bibleBookId, int chapter, CancellationToken token = default);

    /// <summary>Deletes all mapping rows of the verse's German words and inserts <paramref name="rows"/>, in one transaction.</summary>
    Task ReplaceMappingAsync(bool hebrew, int bibleBookId, int chapter, int verse, IReadOnlyList<VerseMappingRow> rows, CancellationToken token = default);
}
