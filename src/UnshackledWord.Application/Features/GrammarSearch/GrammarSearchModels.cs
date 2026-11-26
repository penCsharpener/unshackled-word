using UnshackledWord.Domain.WebApi.Elberfelder.SearchGrammar;

namespace UnshackledWord.Application.Features.GrammarSearch;

/// <summary>A lemma a word token was read as (Strong's base key like <c>G0026</c>).</summary>
public sealed record GrammarLemma(string Key, string Lemma, string Transliteration, string Gloss);

/// <summary>One slot as the repository queries it. Null = no restriction: <see cref="Codes"/> null means any code, <see cref="StrongsKeys"/> null any lemma.</summary>
public sealed record GrammarSlotQuery(IReadOnlyList<string>? Codes, IReadOnlyList<string>? StrongsKeys);

/// <summary>A verse in which every slot has a candidate. <see cref="Shared"/>: some word is a candidate of two slots (the distinct-word rule has to be checked).</summary>
public sealed class GrammarVerseRow
{
    public int HebRefId { get; init; }
    public bool Shared { get; init; }

    /// <summary>Distinct candidate words of all slots in this verse.</summary>
    public int Words { get; init; }
}

/// <summary>One (slot, original word) candidate of a verse with the word's details.</summary>
public sealed class GrammarMatchRow
{
    public int Slot { get; init; }
    public int HebRefId { get; init; }
    public int WordId { get; init; }
    public int Position { get; init; }
    public string Greek { get; init; } = string.Empty;
    public string Lemma { get; init; } = string.Empty;
    public string Strongs { get; init; } = string.Empty;
    public string Morphology { get; init; } = string.Empty;
}

public sealed class GrammarGermanWordRow
{
    public int HebRefId { get; init; }
    public int PositionInVerse { get; init; }
    public string WordInContext { get; init; } = string.Empty;
}

public sealed class GrammarMappingRow
{
    public int StepWordId { get; init; }
    public int PositionInVerse { get; init; }
}

/// <summary>Dapper access of the grammar search (Greek words keyed by Elb1871 verse).</summary>
public interface IGrammarSearchRepository
{
    /// <summary>Verses where every slot has a candidate word, with the shared-word flag; unordered.</summary>
    Task<IReadOnlyList<GrammarVerseRow>> FindVersesAsync(IReadOnlyList<GrammarSlotQuery> slots, CancellationToken token = default);

    /// <summary>Candidate words per slot in the given verses.</summary>
    Task<IReadOnlyList<GrammarMatchRow>> ReadMatchesAsync(IReadOnlyList<GrammarSlotQuery> slots, IReadOnlyList<int> hebRefIds, CancellationToken token = default);

    /// <summary>Candidate words per slot in the verses <c>fromHebRefId..toHebRefId</c> (inclusive; one chapter).</summary>
    Task<IReadOnlyList<GrammarMatchRow>> ReadMatchesInRangeAsync(IReadOnlyList<GrammarSlotQuery> slots, int fromHebRefId, int toHebRefId, CancellationToken token = default);

    /// <summary>The nearest verses (at most <paramref name="limit"/>) before (<c>HebRefId &lt; boundary</c>, descending) or after (<c>&gt;</c>, ascending) the boundary where every slot has a candidate.</summary>
    Task<IReadOnlyList<GrammarVerseRow>> FindVersesNearAsync(IReadOnlyList<GrammarSlotQuery> slots, int boundaryHebRefId, bool before, int limit, CancellationToken token = default);

    Task<IReadOnlyList<GrammarGermanWordRow>> ReadGermanWordsAsync(IReadOnlyList<int> hebRefIds, CancellationToken token = default);

    /// <summary>German word positions mapped to the given original words.</summary>
    Task<IReadOnlyList<GrammarMappingRow>> ReadMappingsAsync(IReadOnlyList<int> stepWordIds, CancellationToken token = default);
}

/// <summary>Reads a word token as lemmas (Strong's number, Greek script, transliteration). Empty = unknown word.</summary>
public interface IGrammarWordReader
{
    Task<IReadOnlyList<GrammarLemma>> ReadGreekAsync(string token, GrammarWordMode mode, CancellationToken ct = default);
}

/// <remarks>Kept apart from <see cref="GrammarOutcome{T}"/> only for the search endpoint of 0029.1.</remarks>
public sealed class GrammarSearchResult
{
    public SearchGrammarResponse? Response { get; init; }
    public GrammarQueryError? Error { get; init; }
}

/// <summary>Either the <see cref="Response"/> or the <see cref="Error"/> of a query that cannot be searched.</summary>
public sealed class GrammarOutcome<T> where T : class
{
    public T? Response { get; init; }
    public GrammarQueryError? Error { get; init; }
}

public interface IGrammarSearchService
{
    Task<GrammarSearchResult> SearchAsync(string? query, int page, int? bookId, CancellationToken token = default);

    /// <summary>All hits of the query in one chapter: German positions and original word ids per verse (roadmap 0029.2).</summary>
    Task<GrammarOutcome<SearchGrammarChapterResponse>> ChapterAsync(string? query, int bibleBookId, int chapter, CancellationToken token = default);

    /// <summary>The nearest chapters before and after the given one that have a hit (roadmap 0029.2).</summary>
    Task<GrammarOutcome<SearchGrammarNeighboursResponse>> NeighboursAsync(string? query, int bibleBookId, int chapter, CancellationToken token = default);
}
