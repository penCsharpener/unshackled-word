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
