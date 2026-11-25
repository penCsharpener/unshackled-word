namespace UnshackledWord.Application.Features.VerseSimilarity;

/// <summary>An Elberfelder verse of the requested range with its STEP key (roadmap 0027.3).</summary>
public sealed class SimilarSourceVerse
{
    public int HebRefId { get; set; }
    public int LxxRefId { get; set; }
}

/// <summary>One stored hit with its target resolved to the Elberfelder verse. <c>TargetHebRefId</c>/<c>TargetText</c> are null when unresolved.</summary>
public sealed class SimilarHitRow
{
    public int SourceHebRefId { get; set; }
    public int SourceLxxRefId { get; set; }
    public int TargetLxxRefId { get; set; }
    public int? TargetHebRefId { get; set; }
    public string? TargetText { get; set; }
    public int Rank { get; set; }
    public decimal Score { get; set; }
    public int SharedCount { get; set; }
    public int PhraseCount { get; set; }
    public int RunId { get; set; }
}

public sealed record SimilarVersePage(IReadOnlyList<SimilarSourceVerse> Sources, IReadOnlyList<SimilarHitRow> Hits);
