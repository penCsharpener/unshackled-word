namespace UnshackledWord.Domain.Models.Dbo.VerseSimilarity;

/// <summary>One ranked pair of the current run (table <c>VerseSimilarities</c>, migration 0049, roadmap 0027), keyed by STEP <c>LxxRefId</c>.</summary>
public sealed class VerseSimilarityDbo
{
    public const string DboName = "\"unshackled-word\".\"VerseSimilarities\"";

    public int SourceLxxRefId { get; set; }
    public int TargetLxxRefId { get; set; }

    /// <summary>1..TopK within the source verse.</summary>
    public short Rank { get; set; }

    public decimal Score { get; set; }
    public short SharedCount { get; set; }
    public short PhraseCount { get; set; }
    public int RunId { get; set; }
}
