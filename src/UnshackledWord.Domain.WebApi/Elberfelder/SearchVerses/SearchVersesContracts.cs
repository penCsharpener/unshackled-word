namespace UnshackledWord.Domain.WebApi.Elberfelder.SearchVerses;

public sealed class SearchVersesRequest
{
    public const int MaxQueryLength = 200;
    public const int MaxWords = 8;
    public const int MaxWordLength = 50;
    public const int DefaultPageSize = 20;
    public const int MinPageSize = 10;
    public const int MaxPageSize = 50;

    /// <summary>Search text: words are AND-ed, "..." is a phrase, a leading - excludes, oder / or separates alternatives.</summary>
    public string Q { get; set; } = string.Empty;

    /// <summary>1-based.</summary>
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = DefaultPageSize;

    /// <summary>Optional Elberfelder book (1-66) the hits are restricted to; empty (or 0) means all books. The per-book counts ignore it.</summary>
    public int? BookId { get; set; }

    /// <summary>Reader level 0-1000 (roadmap 0011): a verse also matches by the revision variant that level shows; 200 = default, 0 = Elb1871 only.</summary>
    public int Level { get; set; } = 200;

    public const int BaseLevel = 0;
}

public sealed class SearchVersesResponse
{
    /// <summary>The trimmed query text as searched.</summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>Verses matching the query (within <c>bookId</c> when given).</summary>
    public int Total { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }

    /// <summary>The searched (not excluded) words of the query with the lexemes and lemmas they were matched by.</summary>
    public List<SearchVersesToken> Tokens { get; set; } = [];

    /// <summary>Hits per book for the whole query, independent of <c>bookId</c> and paging; ordered by book.</summary>
    public List<SearchVersesBookCount> BookCounts { get; set; } = [];

    /// <summary>Relevance order: literal hits first, then lemma-only hits; ties in Bible order.</summary>
    public List<SearchVersesHit> Hits { get; set; } = [];
}

public sealed class SearchVersesToken
{
    /// <summary>The word as typed (lower-cased).</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Stemmed lexeme(s) of the word (one per letter run for words with hyphen or apostrophe).</summary>
    public List<string> Lexemes { get; set; } = [];

    /// <summary>Lemmas the word was widened by ("ging" also finds forms of "gehen"); omitted when it was not widened.</summary>
    public List<string>? Lemmas { get; set; }
}

public sealed class SearchVersesBookCount
{
    public int BookId { get; set; }

    public int Count { get; set; }
}

public sealed class SearchVersesHit
{
    public int BibleBookId { get; set; }

    public int Chapter { get; set; }

    public int Verse { get; set; }

    /// <summary>The whole verse in word order; <c>matched</c> marks the words that satisfy the query.</summary>
    public List<SearchVersesHitToken> Tokens { get; set; } = [];

    /// <summary>True when the tokens are the revised wording (the revision variant matched); omitted for Elb1871 text.</summary>
    public bool? Revised { get; set; }

    /// <summary>Level of the revision variant shown; omitted for Elb1871 text.</summary>
    public int? Level { get; set; }
}

public sealed class SearchVersesHitToken
{
    /// <summary>The word as written in the verse (with punctuation).</summary>
    public string Text { get; set; } = string.Empty;

    public bool Matched { get; set; }
}

/// <summary><c>GET api/elberfelder/search/verses/chapter/{bibleBookId}/{chapter}?q=&amp;level=</c> (roadmap 0029.10): the hits of a verse query in one chapter, for marking in the reader.</summary>
public sealed class SearchVersesChapterRequest
{
    public int BibleBookId { get; set; }

    public int Chapter { get; set; }

    public string Q { get; set; } = string.Empty;

    /// <summary>Reader level 0-1000 as in <see cref="SearchVersesRequest.Level"/>; the positions refer to the text this level shows.</summary>
    public int Level { get; set; } = 200;
}

public sealed class SearchVersesChapterResponse
{
    /// <summary>Hit verses of the chapter in verse order (a verse without a matching word is left out).</summary>
    public List<SearchVersesChapterVerse> Verses { get; set; } = [];

    /// <summary>Matched words in the chapter.</summary>
    public int Total { get; set; }
}

public sealed class SearchVersesChapterVerse
{
    public int Verse { get; set; }

    /// <summary>Set when the positions refer to the revision variant of the verse (<c>RevisionWords.PositionInVerse</c>); omitted for Elb1871 positions (<c>Elb1871Words.PositionInVerse</c>).</summary>
    public bool? Revised { get; set; }

    /// <summary>Positions of the matched words in the verse, ascending.</summary>
    public List<int> Positions { get; set; } = [];
}

/// <summary><c>GET api/elberfelder/search/verses/neighbours?q=&amp;level=&amp;bibleBookId=&amp;chapter=</c> (roadmap 0029.10).</summary>
public sealed class SearchVersesNeighboursRequest
{
    public string Q { get; set; } = string.Empty;

    public int Level { get; set; } = 200;

    public int BibleBookId { get; set; }

    public int Chapter { get; set; }
}

public sealed class SearchVersesNeighboursResponse
{
    /// <summary>The nearest chapter before with a hit; omitted when there is none.</summary>
    public SearchVersesChapterRef? Previous { get; set; }

    /// <summary>The nearest chapter after with a hit; omitted when there is none.</summary>
    public SearchVersesChapterRef? Next { get; set; }
}

public sealed class SearchVersesChapterRef
{
    public int BibleBookId { get; set; }

    public int Chapter { get; set; }
}
