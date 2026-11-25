namespace UnshackledWord.Domain.WebApi.Maintainer;

/// <summary>The four rebuild parameters (roadmap 0027); the same bounds apply to the request, see <c>VerseSimilarityRebuildService.Validate</c>.</summary>
public sealed class VerseSimilarityParametersResponse
{
    /// <summary>A Strong's in more than this share of a testament's verses is a stop word.</summary>
    public double StopDfShare { get; set; }

    /// <summary>Hits kept per source verse.</summary>
    public int TopK { get; set; }

    /// <summary>Lowest score kept.</summary>
    public double MinScore { get; set; }

    /// <summary>Weight of the phrase (bigram) bonus.</summary>
    public double PhraseWeight { get; set; }
}

/// <summary>One row of <c>VerseSimilarityRuns</c>.</summary>
public sealed class VerseSimilarityRunResponse
{
    public int Id { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary><c>running</c>, <c>succeeded</c> or <c>failed</c>.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>User id of the maintainer; null = SeedDb.</summary>
    public int? StartedBy { get; set; }

    /// <summary>Display name of <see cref="StartedBy"/>; null for SeedDb or a deleted user.</summary>
    public string? StartedByName { get; set; }

    public VerseSimilarityParametersResponse Parameters { get; set; } = new();
    public int? PairCount { get; set; }
    public int? StopWordCount { get; set; }
    public int? DurationMs { get; set; }

    /// <summary>Raw JSON of the evaluation (recall@30, golden pair ranks); null for runs before roadmap 0027.6 and for failed runs.</summary>
    public string? Evaluation { get; set; }

    /// <summary><see cref="Evaluation"/> parsed (roadmap 0027.6); null when there is none or it cannot be read.</summary>
    public VerseSimilarityEvaluationResponse? EvaluationResult { get; set; }

    public string? Error { get; set; }
}

/// <summary>Recall of the raw TSK links of one testament in the stored top lists (roadmap 0027.6).</summary>
public sealed class VerseSimilarityTestamentRecallResponse
{
    /// <summary><c>old</c> or <c>new</c>.</summary>
    public string Testament { get; set; } = string.Empty;

    /// <summary>TSK links considered (distinct, both ends in the testament).</summary>
    public int Links { get; set; }

    public int HitsAt10 { get; set; }
    public int HitsAt30 { get; set; }

    /// <summary>Share 0..1 of <see cref="Links"/> whose target is among the source's first 10 hits.</summary>
    public double RecallAt10 { get; set; }

    /// <summary>Share 0..1 of <see cref="Links"/> whose target is among the source's first 30 hits.</summary>
    public double RecallAt30 { get; set; }
}

/// <summary>Rank of a golden pair in both directions; null = not among the stored hits ("fehlt").</summary>
public sealed class VerseSimilarityGoldenRankResponse
{
    /// <summary>German citation, e.g. <c>Hebr 4,12</c>.</summary>
    public string Source { get; set; } = string.Empty;

    public int SourceVerseId { get; set; }
    public string Target { get; set; } = string.Empty;
    public int TargetVerseId { get; set; }

    /// <summary>Rank of the target in the source's hits.</summary>
    public int? Rank { get; set; }

    /// <summary>Rank of the source in the target's hits.</summary>
    public int? ReverseRank { get; set; }
}

/// <summary>Evaluation of a run against TSK and the golden pairs (roadmap 0027.6).</summary>
public sealed class VerseSimilarityEvaluationResponse
{
    public int TopK { get; set; }
    public List<VerseSimilarityTestamentRecallResponse> Testaments { get; set; } = [];
    public List<VerseSimilarityGoldenRankResponse> GoldenPairs { get; set; } = [];

    /// <summary>Set when the evaluation failed (the run itself succeeded).</summary>
    public string? Error { get; set; }
}

/// <summary><c>GET api/maintainer/verse-similarity</c>.</summary>
public sealed class VerseSimilarityStatusResponse
{
    /// <summary>The configured defaults (section <c>VerseSimilarity</c>).</summary>
    public VerseSimilarityParametersResponse Defaults { get; set; } = new();

    /// <summary>The last five runs, newest first.</summary>
    public List<VerseSimilarityRunResponse> Runs { get; set; } = [];

    /// <summary>True while a rebuild runs (this API's job or another process holding the rebuild lock, e.g. SeedDb).</summary>
    public bool Running { get; set; }

    /// <summary>The running or last job of this API process (log lines as progress); null when none ran since the start.</summary>
    public SeedJobResponse? Job { get; set; }
}

/// <summary>Body of <c>POST api/maintainer/verse-similarity/rebuild</c>; every null parameter takes the configured default.</summary>
public sealed class StartVerseSimilarityRebuildRequest
{
    public double? StopDfShare { get; set; }
    public int? TopK { get; set; }
    public double? MinScore { get; set; }
    public double? PhraseWeight { get; set; }
}

/// <summary>202 answer of <c>POST api/maintainer/verse-similarity/rebuild</c>.</summary>
public sealed class StartVerseSimilarityRebuildResponse
{
    /// <summary>Operation name of rebuild jobs in <see cref="SeedJobResponse.Operation"/>.</summary>
    public const string Operation = "verseSimilarity";

    public SeedJobResponse Job { get; set; } = new();
}
