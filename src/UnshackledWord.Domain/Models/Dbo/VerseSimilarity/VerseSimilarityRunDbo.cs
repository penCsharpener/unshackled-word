namespace UnshackledWord.Domain.Models.Dbo.VerseSimilarity;

/// <summary>One verse similarity rebuild (table <c>VerseSimilarityRuns</c>, migration 0049, roadmap 0027).</summary>
public class VerseSimilarityRunDbo
{
    public const string DboName = "\"unshackled-word\".\"VerseSimilarityRuns\"";

    public const string StatusRunning = "running";
    public const string StatusSucceeded = "succeeded";
    public const string StatusFailed = "failed";

    public int Id { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary><see cref="StatusRunning"/>, <see cref="StatusSucceeded"/> or <see cref="StatusFailed"/>.</summary>
    public string Status { get; set; } = default!;

    /// <summary>User id of the maintainer; null = SeedDb.</summary>
    public int? StartedBy { get; set; }

    public decimal StopDfShare { get; set; }
    public short TopK { get; set; }
    public decimal MinScore { get; set; }
    public decimal PhraseWeight { get; set; }
    public int? PairCount { get; set; }
    public int? StopWordCount { get; set; }
    public int? DurationMs { get; set; }

    /// <summary>JSON (recall@30, golden pair ranks), filled from roadmap 0027.6.</summary>
    public string? Evaluation { get; set; }

    public string? Error { get; set; }
}
