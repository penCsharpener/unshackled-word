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
