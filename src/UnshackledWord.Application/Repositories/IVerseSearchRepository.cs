using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Application.Repositories;

/// <summary>Verse full text search over Elb1871Verses."SearchVector" (text search configuration uw_german, see migrations 0015 and 0016).</summary>
public interface IVerseSearchRepository
{
    /// <summary>The Elb1871 level (roadmap 0011): searching at it reads Elb1871Verses only.</summary>
    const int BaseLevel = 0;

    /// <summary>Stem lexeme of each lower-cased letter run (ts_lexize on the dictionary without stop words); a word the dictionary drops has a null lexeme.</summary>
    Task<IReadOnlyList<VerseSearchLexemeRow>> GetLexemesAsync(IReadOnlyList<string> words, CancellationToken token = default);

    /// <summary>Matching verses per book for the whole query (no book filter, no paging). Below or above <see cref="BaseLevel"/> a verse also matches by the revision variant the level shows.</summary>
    Task<IReadOnlyList<VerseSearchBookCountRow>> GetBookCountsAsync(string tsQuery, int level, CancellationToken token = default);

    /// <summary>One page of matching verses in relevance order: literal matches (<paramref name="literalTsQuery"/>, null when the query has no lemma part) first, then rank, then Bible order. One row per verse; RevisionVerseId is set when the shown text is the revision variant.</summary>
    Task<IReadOnlyList<VerseSearchHitRow>> GetHitsAsync(string tsQuery, string? literalTsQuery, int level, int firstRefId, int lastRefId, int offset, int limit, CancellationToken token = default);

    /// <summary>The words of the given verses with the stems of the plain word and the morphology lemma, in verse and word order.</summary>
    Task<IReadOnlyList<VerseSearchWordRow>> GetWordsAsync(IReadOnlyList<int> hebRefIds, CancellationToken token = default);

    /// <summary>The words of revision variants (stems of the plain word, no lemma), keyed by the variant's HebRefId.</summary>
    Task<IReadOnlyList<VerseSearchWordRow>> GetRevisionWordsAsync(IReadOnlyList<int> revisionVerseIds, CancellationToken token = default);
}
