using UnshackledWord.Domain.WebApi.Elberfelder.SearchVerses;

namespace UnshackledWord.Application.Features.VerseSearch;

public interface IVerseSearchService
{
    /// <summary>Null when the query has no searchable word (the validator rejects those before this is called).</summary>
    Task<SearchVersesResponse?> SearchAsync(string query, int page, int pageSize, int? bookId, CancellationToken token = default);
}
