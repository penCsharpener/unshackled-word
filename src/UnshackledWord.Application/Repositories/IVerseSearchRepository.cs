using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Application.Repositories;

/// <summary>Verse full text search over Elb1871Verses."SearchVector" (text search configuration uw_german, see migrations 0015 and 0016).</summary>
public interface IVerseSearchRepository
{
    /// <summary>Stem lexeme of each lower-cased letter run (ts_lexize on the dictionary without stop words); a word the dictionary drops has a null lexeme.</summary>
    Task<IReadOnlyList<VerseSearchLexemeRow>> GetLexemesAsync(IReadOnlyList<string> words, CancellationToken token = default);

    /// <summary>Matching verses per book for the whole query (no book filter, no paging).</summary>
    Task<IReadOnlyList<VerseSearchBookCountRow>> GetBookCountsAsync(string tsQuery, CancellationToken token = default);

    /// <summary>One page of matching verses in relevance order: literal matches (<paramref name="literalTsQuery"/>, null when the query has no lemma part) first, then rank, then Bible order.</summary>
    Task<IReadOnlyList<VerseSearchHitRow>> GetHitsAsync(string tsQuery, string? literalTsQuery, int firstRefId, int lastRefId, int offset, int limit, CancellationToken token = default);

    /// <summary>The words of the given verses with the stems of the plain word and the morphology lemma, in verse and word order.</summary>
    Task<IReadOnlyList<VerseSearchWordRow>> GetWordsAsync(IReadOnlyList<int> hebRefIds, CancellationToken token = default);
}
