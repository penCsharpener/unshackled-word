namespace UnshackledWord.Domain.WebApi.Elberfelder.GetStrongsSummary;

public sealed class GetStrongsSummaryResponse
{
    /// <summary>Normalized request, e.g. "G1577", "G3004" or "H0430G".</summary>
    public string Strongs { get; set; } = string.Empty;

    /// <summary>"greek", "hebrew" or "aramaic".</summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>Original word of the first lexicon variant.</summary>
    public string? Lemma { get; set; }

    public string? Transliteration { get; set; }

    /// <summary>English; the distinct variant glosses joined with "; ".</summary>
    public string? Gloss { get; set; }

    /// <summary>Sum of <see cref="ByBook"/> counts.</summary>
    public int OccurrenceCount { get; set; }

    /// <summary>Sum of <see cref="ByBook"/> verse counts (a verse never spans books).</summary>
    public int VerseCount { get; set; }

    /// <summary>Occurrences with at least one non-added Elberfelder word.</summary>
    public int MappedCount { get; set; }

    public int UnmappedCount { get; set; }

    /// <summary>Only books with at least one occurrence, ascending.</summary>
    public List<StrongsBookCountResponse> ByBook { get; set; } = [];

    /// <summary>Every Elberfelder rendering, most used first; <see cref="StrongsRenderingResponse.Key"/> is the filter value of the concordance.</summary>
    public List<StrongsRenderingResponse> Renderings { get; set; } = [];

    /// <summary>Only set when the request matched more than one lexicon key (number given without a letter).</summary>
    public List<StrongsVariantResponse>? Variants { get; set; }
}

public sealed class StrongsBookCountResponse
{
    public int BibleBookId { get; set; }
    public int Count { get; set; }
    public int VerseCount { get; set; }
}

public sealed class StrongsRenderingResponse
{
    public string Word { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class StrongsVariantResponse
{
    public string Strongs { get; set; } = string.Empty;
    public string Lemma { get; set; } = string.Empty;
    public string? Transliteration { get; set; }
    public string? Gloss { get; set; }
    public int Count { get; set; }
}
