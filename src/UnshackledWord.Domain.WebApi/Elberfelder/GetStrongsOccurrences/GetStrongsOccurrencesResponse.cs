namespace UnshackledWord.Domain.WebApi.Elberfelder.GetStrongsOccurrences;

public sealed class GetStrongsOccurrencesResponse
{
    /// <summary>Normalized Strong's number, e.g. "G0026".</summary>
    public string Strongs { get; set; } = string.Empty;

    /// <summary>"greek" or "hebrew".</summary>
    public string Language { get; set; } = string.Empty;

    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public List<StrongsOccurrenceResponse> Items { get; set; } = [];
}

public sealed class StrongsOccurrenceResponse
{
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }

    /// <summary>The Greek or Hebrew word as written in the verse.</summary>
    public string? Original { get; set; }

    /// <summary>Elberfelder words of the verse before the keyword, in verse order.</summary>
    public List<StrongsOccurrenceTokenResponse> Before { get; set; } = [];

    /// <summary>
    /// From the first to the last Elberfelder word mapped to this original word, so words in between stay visible
    /// (<c>Matched</c> false). Empty when the original word is not mapped; the whole verse is then in <see cref="After"/>.
    /// </summary>
    public List<StrongsOccurrenceTokenResponse> Keyword { get; set; } = [];

    /// <summary>Punctuation that trailed the last keyword word; shown outside the keyword box. Omitted when there is none.</summary>
    public string? KeywordSuffix { get; set; }

    /// <summary>Elberfelder words of the verse after the keyword, in verse order.</summary>
    public List<StrongsOccurrenceTokenResponse> After { get; set; } = [];
}

public sealed class StrongsOccurrenceTokenResponse
{
    public string Text { get; set; } = string.Empty;

    /// <summary>True when the word translates the original word of this row.</summary>
    public bool Matched { get; set; }
}
