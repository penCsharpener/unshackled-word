namespace UnshackledWord.Domain.WebApi.Revision;

// Request/response bodies of POST api/revision/bulkReplace/search (roadmap 0013). camelCase JSON, nulls omitted.

/// <summary>Finds every occurrence of a German word in the text of the source revision (own variant of the verse, else Elb1871).</summary>
public sealed class BulkReplaceSearchRequest
{
    /// <summary>One token, 2-40 letters; compared whole-word and case-insensitively on <c>PlainWord</c>.</summary>
    public string Word { get; set; } = string.Empty;

    /// <summary>Also match every token whose Elb1871 lemma is a lemma of <see cref="Word"/>.</summary>
    public bool SameLemma { get; set; }

    /// <summary>Revision <c>code</c> whose text is searched (base <c>elb1871</c> allowed).</summary>
    public string SourceRevision { get; set; } = string.Empty;

    /// <summary>Revision <c>code</c> the changes will be saved to (writable, not the base); only used for the target flags of each row.</summary>
    public string TargetRevision { get; set; } = string.Empty;
}

public sealed class BulkReplaceSearchResponse
{
    /// <summary>Lemmas that <see cref="BulkReplaceSearchRequest.SameLemma"/> used (empty when it was off).</summary>
    public List<string> Lemmas { get; set; } = [];

    /// <summary>Number of rows returned.</summary>
    public int Total { get; set; }

    /// <summary>True when more than <see cref="BulkReplaceLimits.MaxRows"/> occurrences exist and the list was cut.</summary>
    public bool Capped { get; set; }

    /// <summary>One entry per Strong's number among the hits, most frequent first; <c>Strongs</c> null = hits without a link.</summary>
    public List<BulkReplaceFacet> Facets { get; set; } = [];

    public List<BulkReplaceRow> Rows { get; set; } = [];
}

public static class BulkReplaceLimits
{
    public const int MaxRows = 1000;
    public const int ContextWords = 6;
}

public sealed class BulkReplaceFacet
{
    public string? Strongs { get; set; }
    public int Count { get; set; }
}

public sealed class BulkReplaceRow
{
    public int HebRefId { get; set; }

    /// <summary>1-based number of this hit among the hits of the same verse.</summary>
    public int Occurrence { get; set; }

    /// <summary>Position of the token in the verse of the source text.</summary>
    public int PositionInVerse { get; set; }

    /// <summary>The token as written in the verse (<c>WordInContext</c>, with punctuation).</summary>
    public string Found { get; set; } = string.Empty;

    public string ContextBefore { get; set; } = string.Empty;
    public string ContextAfter { get; set; } = string.Empty;
    public List<string> Strongs { get; set; } = [];
    public string? Lemma { get; set; }

    /// <summary>The word before the hit when it is an article or determiner; null otherwise.</summary>
    public string? ArticleBefore { get; set; }

    /// <summary>Active version of the target revision for this verse; null when the target has no variant.</summary>
    public int? TargetVersion { get; set; }

    /// <summary>The target variant's text differs from the source text of this verse.</summary>
    public bool TargetDiffers { get; set; }
}
