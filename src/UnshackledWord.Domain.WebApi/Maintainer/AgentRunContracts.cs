namespace UnshackledWord.Domain.WebApi.Maintainer;

/// <summary>The verse range and settings of an agent run (roadmap 0016.6): shared by estimate and start.</summary>
public class AgentRunRangeRequest
{
    /// <summary>BibleBookId 1..66.</summary>
    public int Book { get; set; }

    public int FromChapter { get; set; }
    public int FromVerse { get; set; }

    /// <summary>Defaults to <see cref="FromChapter"/>.</summary>
    public int? ToChapter { get; set; }

    /// <summary>Last verse (inclusive); defaults to the end of the chapter when <see cref="ToChapter"/> is set, otherwise to <see cref="FromVerse"/>.</summary>
    public int? ToVerse { get; set; }

    /// <summary>Provider profile name from <c>AgentMapping:Profiles</c>.</summary>
    public string Profile { get; set; } = string.Empty;

    /// <summary>Model tag; defaults to the profile's model.</summary>
    public string? Model { get; set; }

    /// <summary><c>redo</c> (default) or <c>fill-open</c>.</summary>
    public string? Mode { get; set; }

    /// <summary>Mode <c>lxx-tag</c> only (roadmap 0028.7f): also tag units whose words all have confirmed tags (default false: they are skipped).</summary>
    public bool IncludeConfirmed { get; set; }

    /// <summary>Mode <c>lxx-tag</c> only (Daniel): <c>og</c> or <c>th</c> restricts the run to that text form; null covers both.</summary>
    public string? TextForm { get; set; }
}

public sealed class EstimateAgentRunRequest : AgentRunRangeRequest
{
}

public sealed class StartAgentRunRequest : AgentRunRangeRequest
{
    /// <summary>Overrides the profile's <c>MaxTokensPerRun</c> for this run.</summary>
    public long? MaxTokens { get; set; }

    /// <summary>Overrides the profile's <c>MaxCostPerRun</c> (USD) for this run.</summary>
    public decimal? MaxCostUsd { get; set; }
}

public sealed class ResumeAgentRunRequest
{
    public int RunId { get; set; }

    /// <summary>New token cap (must exceed what is already spent); null keeps the stored cap.</summary>
    public long? MaxTokens { get; set; }

    public decimal? MaxCostUsd { get; set; }
}

public sealed class AgentRunIdRequest
{
    public int RunId { get; set; }
}

public sealed class ListAgentRunsRequest
{
    public int? Limit { get; set; }
}

public sealed class AgentRunEstimateResponse
{
    public string Profile { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public int Packages { get; set; }

    /// <summary>Verses the model is asked about (verses where everything is fixed are not sent).</summary>
    public int Verses { get; set; }

    public int SkippedVerses { get; set; }
    public int PrefixTokens { get; set; }

    public long EstimatedInputTokens { get; set; }
    public long EstimatedCachedInputTokens { get; set; }
    public long EstimatedOutputTokens { get; set; }

    /// <summary>Input plus output including the 30 % retry margin: the number compared with the cap.</summary>
    public long EstimatedTotalTokens { get; set; }

    public decimal EstimatedCostUsd { get; set; }
    public long? MaxTokens { get; set; }
    public decimal? MaxCostUsd { get; set; }

    /// <summary>The estimate is above a cap: the run would stop with <c>budget-exceeded</c> (resumable with a raised cap).</summary>
    public bool ExceedsCap { get; set; }

    public AgentTokenStats Stats { get; set; } = new();
    public List<string> Warnings { get; set; } = [];
}

public sealed class AgentRunDto
{
    public int Id { get; set; }
    public string Profile { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
    public int Book { get; set; }
    public int FromRefId { get; set; }
    public int ToRefId { get; set; }
    public string Mode { get; set; } = string.Empty;

    /// <summary>queued, running, completed, failed, cancelled, budget-exceeded.</summary>
    public string Status { get; set; } = string.Empty;

    public string? StatusNote { get; set; }
    public long InputTokens { get; set; }
    public long CachedInputTokens { get; set; }
    public long OutputTokens { get; set; }
    public decimal CostUsd { get; set; }

    /// <summary>Share of prompt tokens served from the provider cache (0..1); near zero on a long run means the prefix is not cached.</summary>
    public double CacheRate { get; set; }

    public long? EstimatedTokens { get; set; }
    public decimal? EstimatedCostUsd { get; set; }
    public long? MaxTokens { get; set; }
    public decimal? MaxCostUsd { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public int Packages { get; set; }
    public int PackagesCompleted { get; set; }
    public int PackagesFailed { get; set; }
    public int ProposedVerses { get; set; }
    public int FlaggedVerses { get; set; }

    /// <summary>Proposals of the run that still wait for a review.</summary>
    public int OpenProposals { get; set; }

    /// <summary>True while a worker of this API process is running it.</summary>
    public bool Active { get; set; }
}

public sealed class ListAgentRunsResponse
{
    public List<AgentRunDto> Runs { get; set; } = [];
}

public sealed class AgentProfileDto
{
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public bool Usable { get; set; }
    public string? UnusableReason { get; set; }
    public long? MaxTokensPerRun { get; set; }
    public decimal? MaxCostPerRun { get; set; }
    public int? MaxVersesPerPackage { get; set; }
    public int MaxParallel { get; set; }
}
