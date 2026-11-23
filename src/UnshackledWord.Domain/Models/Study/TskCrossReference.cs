namespace UnshackledWord.Domain.Models.Study;

/// <summary>A TSK cross-reference, all reference ids in Elberfelder (Hebrew) numbering.</summary>
public sealed class TskCrossReference
{
    public int SourceHebRefId { get; init; }
    public string Scope { get; init; } = default!;
    public int TargetStartHebRefId { get; init; }
    public int? TargetEndHebRefId { get; init; }

    /// <summary><c>tsk</c> or <c>added</c> (a link TSK lacks, roadmap 0024.4; then <see cref="Scope"/> is empty).</summary>
    public string Origin { get; init; } = "tsk";

    /// <summary>Curation of the link (roadmap 0024.3, <c>TskCurations</c>): <c>highlight</c>, <c>hide</c> or null (neutral, no row).</summary>
    public string? Rating { get; init; }

    /// <summary>Curator comment of the link, plain text, max 500 characters; null when there is none.</summary>
    public string? Comment { get; init; }
}
