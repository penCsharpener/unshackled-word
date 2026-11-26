namespace UnshackledWord.Domain.WebApi.Elberfelder.SearchGrammar;

/// <summary><c>GET api/elberfelder/search/grammar?q=&amp;page=&amp;book=</c> (roadmap 0029.1): grammar query over the Greek NT words, answered per Elb1871 verse.</summary>
public sealed class SearchGrammarRequest
{
    public const int MaxQueryLength = 200;
    public const int PageSize = 50;

    /// <summary>Query such as <c>agape Kasus:nom</c>; see roadmap 0029, query language.</summary>
    public string Q { get; set; } = string.Empty;

    /// <summary>1-based.</summary>
    public int Page { get; set; } = 1;

    /// <summary>Optional Elberfelder book (1-66) the hit list is restricted to; the counts per book ignore it.</summary>
    public int? Book { get; set; }
}

public sealed class SearchGrammarResponse
{
    public List<GrammarSlotResponse> Slots { get; set; } = [];

    /// <summary>Matching original words in all hit verses.</summary>
    public int TotalWords { get; set; }

    public int TotalVerses { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }

    /// <summary>Hit verses per book (independent of <c>book</c> and paging), ordered by book.</summary>
    public List<GrammarBookCount> Books { get; set; } = [];

    /// <summary>The verses of the page in canonical order.</summary>
    public List<GrammarHit> Hits { get; set; } = [];
}

public sealed class GrammarSlotResponse
{
    /// <summary>The tokens of the slot as typed.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary><c>lemma</c> (word read as Strong's / lemma), <c>german</c> (German lemma, later) or <c>any</c> (filters only).</summary>
    public string ReadAs { get; set; } = "any";

    public List<GrammarLemmaResponse> Lemmas { get; set; } = [];

    public List<GrammarFilterResponse> Filters { get; set; } = [];
}

public sealed class GrammarLemmaResponse
{
    /// <summary>Display key, e.g. <c>G26</c>.</summary>
    public string Strongs { get; set; } = string.Empty;

    public string Lemma { get; set; } = string.Empty;

    public string Transliteration { get; set; } = string.Empty;

    public string Gloss { get; set; } = string.Empty;
}

public sealed class GrammarFilterResponse
{
    /// <summary>Canonical key name, e.g. <c>Kasus</c>.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>German labels of the values (OR); for <c>Code</c> the code patterns.</summary>
    public List<string> Values { get; set; } = [];

    public bool Negated { get; set; }

    /// <summary>True for <c>dt.</c> filters (the German word).</summary>
    public bool German { get; set; }
}

public sealed class GrammarBookCount
{
    public int BibleBookId { get; set; }

    /// <summary>Hit verses in the book.</summary>
    public int Count { get; set; }
}

public sealed class GrammarHit
{
    public int BibleBookId { get; set; }

    public int Chapter { get; set; }

    public int Verse { get; set; }

    /// <summary>The Elb1871 words of the verse.</summary>
    public List<GrammarHitWord> Words { get; set; } = [];

    public List<GrammarMatch> Matches { get; set; } = [];
}

public sealed class GrammarHitWord
{
    public string Text { get; set; } = string.Empty;

    public int Position { get; set; }
}

public sealed class GrammarMatch
{
    /// <summary>0-based index into <c>slots</c>.</summary>
    public int Slot { get; set; }

    /// <summary>Elb1871 word positions mapped to the original word; empty when the word has no German mapping.</summary>
    public List<int> GermanPositions { get; set; } = [];

    public GrammarOriginalWord Original { get; set; } = new();
}

public sealed class GrammarOriginalWord
{
    /// <summary><c>greek</c>, <c>hebrew</c> or <c>aramaic</c> (roadmap 0029.6).</summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>The printed Greek form, or the Hebrew form with vowels and accents.</summary>
    public string Form { get; set; } = string.Empty;

    public string Lemma { get; set; } = string.Empty;

    /// <summary>Display key, e.g. <c>G26</c>.</summary>
    public string Strongs { get; set; } = string.Empty;

    /// <summary>Raw Robinson code (Greek) or OSHB code (Hebrew/Aramaic, all segments).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Decoded parts by group name (<c>partOfSpeech</c>, <c>tense</c>, <c>voice</c>, <c>mood</c>, <c>person</c>, <c>case</c>, <c>number</c>, <c>gender</c>, <c>degree</c>; Hebrew: <c>stem</c>, <c>hebrewForm</c>, <c>state</c>) with the contract key as value; translated by the frontend.</summary>
    public Dictionary<string, string> Keys { get; set; } = [];
}

/// <summary><c>GET api/elberfelder/search/grammar/chapter/{bibleBookId}/{chapter}?q=</c> (roadmap 0029.2): the hits of a query in one chapter. A query of words only is valid.</summary>
public sealed class SearchGrammarChapterRequest
{
    public int BibleBookId { get; set; }

    public int Chapter { get; set; }

    public string Q { get; set; } = string.Empty;
}

public sealed class SearchGrammarChapterResponse
{
    /// <summary>Hit verses of the chapter in verse order.</summary>
    public List<GrammarChapterVerse> Verses { get; set; } = [];

    /// <summary>Matched original words in the chapter.</summary>
    public int Total { get; set; }
}

public sealed class GrammarChapterVerse
{
    public int Verse { get; set; }

    /// <summary>Elb1871 word positions mapped to the matched original words, ascending.</summary>
    public List<int> GermanPositions { get; set; } = [];

    /// <summary>Ids of the matched original words (<c>StepGreekWords.Id</c>, the <c>id</c> of the words in the chapter originals), ascending.</summary>
    public List<int> OriginalWordIds { get; set; } = [];
}

/// <summary><c>GET api/elberfelder/search/grammar/neighbours?q=&amp;bibleBookId=&amp;chapter=</c> (roadmap 0029.2).</summary>
public sealed class SearchGrammarNeighboursRequest
{
    public string Q { get; set; } = string.Empty;

    public int BibleBookId { get; set; }

    public int Chapter { get; set; }
}

public sealed class SearchGrammarNeighboursResponse
{
    /// <summary>The nearest chapter before with a hit; omitted when there is none.</summary>
    public GrammarChapterRef? Previous { get; set; }

    /// <summary>The nearest chapter after with a hit; omitted when there is none.</summary>
    public GrammarChapterRef? Next { get; set; }
}

public sealed class GrammarChapterRef
{
    public int BibleBookId { get; set; }

    public int Chapter { get; set; }
}

/// <summary><c>GET api/elberfelder/search/grammar/facets?q=&amp;slot=&amp;key=</c> (roadmap 0029.5).</summary>
public sealed class SearchGrammarFacetsRequest
{
    /// <summary>Query such as <c>agape Kasus:</c>; a filter without value at the end is allowed.</summary>
    public string Q { get; set; } = string.Empty;

    /// <summary>0-based slot of the parsed query.</summary>
    public int Slot { get; set; }

    /// <summary>Key to count (name or alias); omitted = every key with fixed values.</summary>
    public string? Key { get; set; }
}

public sealed class SearchGrammarFacetsResponse
{
    public int Slot { get; set; }

    public List<GrammarFacet> Facets { get; set; } = [];
}

public sealed class GrammarFacet
{
    /// <summary>Canonical key name, e.g. <c>Kasus</c>.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Values with at least one word, in vocabulary order.</summary>
    public List<GrammarFacetValue> Values { get; set; } = [];
}

public sealed class GrammarFacetValue
{
    /// <summary>Value id as in the vocabulary (<c>GrammarVocabularyValue.Key</c>).</summary>
    public string Key { get; set; } = string.Empty;

    public string LabelDe { get; set; } = string.Empty;

    /// <summary>Original words that satisfy the slot's lemma and its other filters and have this value.</summary>
    public int Count { get; set; }
}

/// <summary>400 body of a query that cannot be searched; the frontend marks <c>token</c> and offers <c>candidates</c>.</summary>
public sealed class GrammarSearchProblem
{
    public int Status { get; set; } = 400;

    public string Title { get; set; } = "The grammar query cannot be searched.";

    /// <summary>unknownKey, unknownValue, ambiguousValue, tooManySlots, tooManyFilters, emptyQuery or unknownWord (facets: also unknownSlot, and unknownKey for a key without fixed values).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>The offending token as typed (omitted for emptyQuery).</summary>
    public string? Token { get; set; }

    /// <summary>German labels (or key names) that would have been valid.</summary>
    public List<string>? Candidates { get; set; }
}
