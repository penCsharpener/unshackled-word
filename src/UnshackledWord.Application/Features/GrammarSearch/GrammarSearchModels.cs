using UnshackledWord.Domain.WebApi.Elberfelder.SearchGrammar;

namespace UnshackledWord.Application.Features.GrammarSearch;

/// <summary>A lemma a word token was read as (Strong's base key like <c>G0026</c>).</summary>
public sealed record GrammarLemma(string Key, string Lemma, string Transliteration, string Gloss)
{
    /// <summary>Hebrew (and Aramaic) lemmas start with <c>H</c>, Greek ones with <c>G</c>.</summary>
    public bool IsHebrew => Key.StartsWith('H');
}

/// <summary>One language of a slot as the repository queries it. Null = no restriction: <see cref="Codes"/> null means any code, <see cref="StrongsKeys"/> null any lemma.</summary>
public sealed record GrammarSide(IReadOnlyList<string>? Codes, IReadOnlyList<string>? StrongsKeys);

/// <summary>One slot as the repository queries it: the Greek and the Hebrew/Aramaic side, null = that language is not searched for this slot.</summary>
public sealed record GrammarSlotQuery(GrammarSide? Greek, GrammarSide? Hebrew);

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

    /// <summary>True for a <c>StepHebrewWords</c> row (Hebrew and Aramaic); false for <c>StepGreekWords</c>. The ids of the two tables overlap but never occur in one verse.</summary>
    public bool IsHebrew { get; init; }

    /// <summary>The printed Greek or Hebrew form.</summary>
    public string Form { get; init; } = string.Empty;
    public string Lemma { get; init; } = string.Empty;
    public string Strongs { get; init; } = string.Empty;
    public string Morphology { get; init; } = string.Empty;
}

/// <summary>Words with one morphology code (roadmap 0029.5).</summary>
public sealed class GrammarCodeCountRow
{
    public string Code { get; init; } = string.Empty;
    public int Count { get; init; }
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

    /// <summary>Number of Greek words per morphology code, restricted to the given Strong's keys (null = all words); the basis of the facet counts.</summary>
    Task<IReadOnlyList<GrammarCodeCountRow>> CountCodesAsync(IReadOnlyList<string>? strongsKeys, CancellationToken token = default);

    /// <summary>The same for the Hebrew/Aramaic words (root Strong's keys, <c>"Grammar"</c> codes).</summary>
    Task<IReadOnlyList<GrammarCodeCountRow>> CountHebrewCodesAsync(IReadOnlyList<string>? strongsKeys, CancellationToken token = default);

    Task<IReadOnlyList<GrammarGermanWordRow>> ReadGermanWordsAsync(IReadOnlyList<int> hebRefIds, CancellationToken token = default);

    /// <summary>German word positions mapped to the given original words.</summary>
    Task<IReadOnlyList<GrammarMappingRow>> ReadMappingsAsync(IReadOnlyList<int> stepWordIds, CancellationToken token = default);

    /// <summary>German word positions mapped to the given Hebrew/Aramaic words (<c>Elb1871HebrewMapping</c>).</summary>
    Task<IReadOnlyList<GrammarMappingRow>> ReadHebrewMappingsAsync(IReadOnlyList<int> stepWordIds, CancellationToken token = default);
}

/// <summary>Reads a word token as lemmas (Strong's number, Greek or Hebrew script, transliteration). Empty = unknown word.</summary>
public interface IGrammarWordReader
{
    Task<IReadOnlyList<GrammarLemma>> ReadAsync(string token, GrammarWordMode mode, CancellationToken ct = default);

    /// <summary>The lemma text (with vowels) of Hebrew base keys like <c>H1254</c>, for the hit lines; keys without a lexicon entry are missing from the result.</summary>
    Task<IReadOnlyDictionary<string, string>> ReadHebrewLemmasAsync(IEnumerable<string> baseKeys, CancellationToken ct = default);
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

    /// <summary>The values of <paramref name="key"/> (null = every key with fixed values) with the number of original words that satisfy slot <paramref name="slot"/> without its own filter on that key (roadmap 0029.5).</summary>
    Task<GrammarOutcome<SearchGrammarFacetsResponse>> FacetsAsync(string? query, int slot, string? key, CancellationToken token = default);
}
