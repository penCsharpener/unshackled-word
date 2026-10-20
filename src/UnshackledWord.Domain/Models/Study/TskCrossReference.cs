namespace UnshackledWord.Domain.Models.Study;

/// <summary>A TSK cross-reference, all reference ids in Elberfelder (Hebrew) numbering.</summary>
public sealed class TskCrossReference
{
    public int SourceHebRefId { get; init; }
    public string Scope { get; init; } = default!;
    public int TargetStartHebRefId { get; init; }
    public int? TargetEndHebRefId { get; init; }
}
