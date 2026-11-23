namespace UnshackledWord.Domain.WebApi.Tsk;

/// <summary>Values of <c>rating</c> (roadmap 0024.3). Null is neutral.</summary>
public static class TskRatings
{
    public const string Highlight = "highlight";
    public const string Hide = "hide";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { Highlight, Hide };
}

/// <summary>Values of <c>origin</c>: <c>tsk</c> (the link exists in TSK) or <c>added</c> (a link TSK lacks, roadmap 0024.4).</summary>
public static class TskOrigins
{
    public const string Tsk = "tsk";
    public const string Added = "added";
}

/// <summary>
/// <c>PUT api/tsk/curation</c>: the full rating/comment state of one link (source verse → target verse or range, Elberfelder numbering, the key the
/// chapter cross references use: <c>targetEndHebRefId</c> omitted for a single verse). A word link set later (0024.5) is kept.
/// </summary>
public sealed class SaveTskCurationRequest
{
    public int SourceHebRefId { get; set; }
    public int TargetStartHebRefId { get; set; }
    public int? TargetEndHebRefId { get; set; }

    /// <summary><c>highlight</c>, <c>hide</c> or null (neutral).</summary>
    public string? Rating { get; set; }

    /// <summary>Plain text, max 500 characters after trimming; empty or whitespace removes the comment.</summary>
    public string? Comment { get; set; }
}

/// <summary>The curation state of one link after the change. A link back at the plain TSK state has no row: then only the key and <c>origin</c> come back.</summary>
public sealed class TskCurationResponse
{
    public int SourceHebRefId { get; set; }
    public int TargetStartHebRefId { get; set; }
    public int? TargetEndHebRefId { get; set; }
    public string Origin { get; set; } = TskOrigins.Tsk;
    public string? Rating { get; set; }
    public string? Comment { get; set; }

    /// <summary>Omitted when the link has no curation row (plain TSK).</summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Word link (roadmap 0024.5); omitted when the link is not attached to a word.</summary>
    public TskWordResponse? Word { get; set; }
}

/// <summary>
/// <c>POST api/tsk/curation/added</c> (roadmap 0024.4): a link TSK lacks. The target is a German reference in Elberfelder numbering
/// ("Röm 8,28-30"): one verse or a range within one chapter.
/// </summary>
public sealed class AddTskCurationRequest
{
    public int SourceHebRefId { get; set; }

    /// <summary>German reference of the target, e.g. "Röm 8,28-30" (same parser as <c>api/references/parse</c>, Elberfelder numbering).</summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>Plain text, max 500 characters after trimming; empty or whitespace is no comment.</summary>
    public string? Comment { get; set; }
}

/// <summary><c>DELETE api/tsk/curation/added</c> (roadmap 0024.4): the key of an added link, in the body like <c>PUT api/tsk/curation</c>.</summary>
public sealed class DeleteTskCurationRequest
{
    public int SourceHebRefId { get; set; }
    public int TargetStartHebRefId { get; set; }
    public int? TargetEndHebRefId { get; set; }
}

/// <summary>Values of <c>word.state</c> (roadmap 0024.5).</summary>
public static class TskWordLinkStates
{
    /// <summary>The Elb1871 words at the positions still match the quote.</summary>
    public const string Ok = "ok";

    /// <summary>The words changed or the positions are gone: positions omitted, the quote is a hint, the link belongs at verse level.</summary>
    public const string Drifted = "drifted";
}

/// <summary>One link of a word-link request: the target verse or range of a link of the request's source verse (TSK row or added link).</summary>
public sealed class TskLinkTargetKey
{
    public int TargetStartHebRefId { get; set; }

    /// <summary>Omitted for a single verse, like the other curation requests.</summary>
    public int? TargetEndHebRefId { get; set; }
}

/// <summary>
/// <c>PUT api/tsk/curation/word</c> (roadmap 0024.5): links 1–100 links of one source verse to the Elb1871 words <c>startPosition..endPosition</c>
/// (<c>Elb1871Words.PositionInVerse</c>) of that source verse. The quote is rebuilt server-side; rating and comment stay.
/// </summary>
public sealed class SaveTskWordLinkRequest
{
    public int SourceHebRefId { get; set; }
    public List<TskLinkTargetKey> Keys { get; set; } = [];
    public int StartPosition { get; set; }
    public int EndPosition { get; set; }
}

/// <summary><c>DELETE api/tsk/curation/word</c> (roadmap 0024.5): removes the word link of 1–100 links of one source verse (key in the JSON body).</summary>
public sealed class DeleteTskWordLinkRequest
{
    public int SourceHebRefId { get; set; }
    public List<TskLinkTargetKey> Keys { get; set; } = [];
}

/// <summary>Answer of both word-link endpoints: the state of every requested link after the change, in request order.</summary>
public sealed class TskWordLinkResponse
{
    public List<TskCurationResponse> Links { get; set; } = [];
}

/// <summary>
/// The word a link is attached to (roadmap 0024.5), on <c>CrossReferenceResponse</c>, <c>CrossReferenceTextResponse</c> and <c>TskCurationResponse</c>.
/// Positions are Elb1871 positions in the source verse; omitted when <c>state</c> is <c>drifted</c>.
/// </summary>
public sealed class TskWordResponse
{
    public int? StartPosition { get; set; }
    public int? EndPosition { get; set; }

    /// <summary>The Elb1871 words at link time, joined by spaces.</summary>
    public string QuotedText { get; set; } = string.Empty;

    /// <summary><c>ok</c> or <c>drifted</c> (<see cref="TskWordLinkStates"/>).</summary>
    public string State { get; set; } = TskWordLinkStates.Ok;

    /// <summary>
    /// Only from the chapter cross references with <c>level</c> &gt; 0, only for an <c>ok</c> link whose source verse has a revision variant at that level:
    /// the variant words whose <c>BasePositionInVerse</c> lies in the range (empty = no variant word carries the marker; show it at verse level).
    /// </summary>
    public List<TskMappedWordResponse>? MappedWords { get; set; }
}

/// <summary>A word of the active revision variant that carries a word-link marker.</summary>
public sealed class TskMappedWordResponse
{
    public int PositionInVerse { get; set; }
    public int BasePositionInVerse { get; set; }
}
