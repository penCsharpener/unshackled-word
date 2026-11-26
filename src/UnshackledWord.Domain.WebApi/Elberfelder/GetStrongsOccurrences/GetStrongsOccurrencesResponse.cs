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

    /// <summary>"lxx" for <c>corpus=lxx</c>; omitted for the NT/OT list.</summary>
    public string? Corpus { get; set; }

    /// <summary>The LXX list (<c>corpus=lxx</c>, confirmed tags only); <see cref="Items"/> is empty then.</summary>
    public List<LxxStrongsOccurrenceResponse>? LxxItems { get; set; }

    /// <summary>Admin/Maintainer only, <c>corpus=lxx</c>: tags with this Strong's number that are still proposed.</summary>
    public int? UnconfirmedCount { get; set; }
}

public sealed class StrongsOccurrenceResponse
{
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }

    /// <summary>The Greek or Hebrew word as written in the verse.</summary>
    public string? Original { get; set; }

    /// <summary>
    /// The whole original verse in verse order; the word of this row has <c>Matched</c> true. Hebrew is in logical order (the page sets right-to-left).
    /// Empty when the verse has no stored original words.
    /// </summary>
    public List<StrongsOccurrenceTokenResponse> OriginalVerse { get; set; } = [];

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

/// <summary>One confirmed LXX tag with its Strong's number (<c>corpus=lxx</c>, roadmap 0028.9).</summary>
public sealed class LxxStrongsOccurrenceResponse
{
    public int LxxWordId { get; set; }
    public int LxxVerseId { get; set; }

    /// <summary>Rahlfs label, e.g. <c>Gen.1:1</c>, <c>1Ki.2:35.1</c>.</summary>
    public string Ref { get; set; } = string.Empty;

    public string TextForm { get; set; } = string.Empty;
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public int Subverse { get; set; }

    /// <summary>The whole Greek verse; the tagged word has <c>Matched</c> true.</summary>
    public List<StrongsOccurrenceTokenResponse> Words { get; set; } = [];

    /// <summary>Elb1871 verse the unit maps to, or the anchor verse of an addition (<see cref="ElbIsAnchor"/>). Omitted when there is none.</summary>
    public LxxElbVerseRef? Elb { get; set; }
}

public sealed class LxxElbVerseRef
{
    public int HebRefId { get; set; }
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public string? Text { get; set; }

    /// <summary>True when the LXX unit is an addition without Hebrew counterpart and this is only the verse it follows.</summary>
    public bool ElbIsAnchor { get; set; }
}
