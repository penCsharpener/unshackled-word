namespace UnshackledWord.Domain.WebApi.Elberfelder.SearchOriginals;

public sealed class SearchOriginalsRequest
{
    public const int MaxQueryLength = 200;
    public const int MaxTokens = 8;
    public const int MaxTokenLength = 50;
    public const int DefaultTop = 5;
    public const int MaxTop = 20;

    /// <summary>The search text; split into German word tokens (letters, inner apostrophes and hyphens), lower-cased, duplicates dropped.</summary>
    public string Q { get; set; } = string.Empty;

    /// <summary>Originals per lemma + PoS (most frequent first).</summary>
    public int Top { get; set; } = DefaultTop;

    /// <summary>Also list originals for high-frequency lemmas (occurrences summed over PoS at or above the threshold of 2,000).</summary>
    public bool IncludeFrequent { get; set; }
}

public sealed class SearchOriginalsResponse
{
    /// <summary>One entry per distinct token, in query order.</summary>
    public List<SearchOriginalsToken> Tokens { get; set; } = [];

    /// <summary>When the stats snapshot was last refreshed.</summary>
    public DateTime StatsRefreshedAt { get; set; }
}

public sealed class SearchOriginalsToken
{
    /// <summary>Lower-cased token as looked up.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Lemma + PoS the token resolves to (a lemma hit, else the lemmas of the form); empty for unknown tokens.</summary>
    public List<SearchOriginalsLemma> Lemmas { get; set; } = [];
}

public sealed class SearchOriginalsLemma
{
    /// <summary>Lower-cased lemma key.</summary>
    public string Lemma { get; set; } = string.Empty;

    /// <summary>Display spelling of the lemma.</summary>
    public string Word { get; set; } = string.Empty;

    public string? Pos { get; set; }

    /// <summary>Occurrences of this lemma + PoS.</summary>
    public int Occurrences { get; set; }

    /// <summary>Occurrences of the lemma summed over all its PoS (the frequency rule uses this).</summary>
    public int LemmaOccurrences { get; set; }

    /// <summary>True when <see cref="LemmaOccurrences"/> is at or above the high-frequency threshold (2,000).</summary>
    public bool HighFrequency { get; set; }

    /// <summary>Empty for high-frequency lemmas unless includeFrequent=true.</summary>
    public List<SearchOriginalResponse> Originals { get; set; } = [];
}

public sealed class SearchOriginalResponse
{
    /// <summary>"H6951", "G1577".</summary>
    public string Strongs { get; set; } = string.Empty;

    /// <summary>"greek", "hebrew" or "aramaic".</summary>
    public string Language { get; set; } = string.Empty;

    public string? Lemma { get; set; }
    public string? Gloss { get; set; }

    /// <summary>Times the lemma + PoS renders this Strong's number.</summary>
    public int Count { get; set; }

    /// <summary>Count / all renderings of the lemma + PoS (0..1).</summary>
    public double Share { get; set; }

    /// <summary>All occurrences of the Strong's number.</summary>
    public int TotalCount { get; set; }
}
