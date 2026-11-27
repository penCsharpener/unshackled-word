using UnshackledWord.Domain.WebApi.Elberfelder.SearchVerses;

namespace UnshackledWord.Application.Features.VerseSearch;

public interface IVerseSearchService
{
    /// <summary>Null when the query has no searchable word (the validator rejects those before this is called).</summary>
    Task<SearchVersesResponse?> SearchAsync(string query, int page, int pageSize, int? bookId, int level, CancellationToken token = default);

    /// <summary>The matched word positions of the verses of one chapter that match the whole query, at the reader level (roadmap 0029.10). Null when the query has no searchable word.</summary>
    Task<SearchVersesChapterResponse?> ChapterAsync(string query, int bibleBookId, int chapter, int level, CancellationToken token = default);

    /// <summary>The nearest chapters before and after the given one with a hit (canonical order). Null when the query has no searchable word.</summary>
    Task<SearchVersesNeighboursResponse?> NeighboursAsync(string query, int bibleBookId, int chapter, int level, CancellationToken token = default);
}
