namespace UnshackledWord.Domain.WebApi.MetaBible.ParseReference;

public sealed class ParseReferenceResponse
{
    public List<ParsedReference> References { get; set; } = [];

    /// <summary>Set when the input looks like a reference but is wrong (for example a chapter out of range). Null for plain text.</summary>
    public ParseReferenceError? Error { get; set; }

    /// <summary>Book ids a typed prefix could mean ("Phili" gives Philipper and Philemon).</summary>
    public List<int> BookCandidates { get; set; } = [];
}

public sealed class ParsedReference
{
    /// <summary>Null for non-canonical books, which are recognised but not linked.</summary>
    public int? BookId { get; set; }

    public bool BookOnly { get; set; }

    /// <summary>German citation style, e.g. "Offb 2,1–5.7.9–10". A whole chapter is verse 0 to 999 in <see cref="Spans"/>.</summary>
    public string Display { get; set; } = string.Empty;

    public List<ParsedSpan> Spans { get; set; } = [];

    /// <summary>Reader deep link to the first group; null without a chapter or for non-canonical books.</summary>
    public string? Path { get; set; }

    /// <summary>One entry per chapter the reference starts in ("Röm 8,28; 12,1-2" has two), each with its own link.</summary>
    public List<ParsedReferenceGroup> Groups { get; set; } = [];
}

public sealed class ParsedSpan
{
    public int StartChapter { get; set; }

    public int StartVerse { get; set; }

    public int EndChapter { get; set; }

    public int EndVerse { get; set; }
}

public sealed class ParsedReferenceGroup
{
    public string Display { get; set; } = string.Empty;

    public string? Path { get; set; }
}

public sealed class ParseReferenceError
{
    public string Code { get; set; } = string.Empty;

    public int Position { get; set; }

    public Dictionary<string, string> Args { get; set; } = [];
}
