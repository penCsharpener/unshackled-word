namespace UnshackledWord.Application.Features.AgentMapping;

public static class AgentRunStatuses
{
    public const string Queued = "queued";
    public const string Running = "running";
    public const string WaitingForProvider = "waiting-for-provider";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
    public const string BudgetExceeded = "budget-exceeded";

    public static bool IsActive(string status) => status is Queued or Running or WaitingForProvider;

    /// <summary>Statuses a maintainer can continue with "resume".</summary>
    public static bool CanResume(string status) => status is Failed or Cancelled or BudgetExceeded;
}

/// <summary>A run row with the totals the status page shows.</summary>
public sealed class AgentRunRecord
{
    public int Id { get; set; }
    public int CreatedByUserId { get; set; }
    public int AiUserId { get; set; }

    /// <summary>MCP token that built the packages of the run; null for worker and debug runs.</summary>
    public int? ApiTokenId { get; set; }

    public string Profile { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
    public int BibleBookId { get; set; }
    public int FromRefId { get; set; }
    public int ToRefId { get; set; }
    public string Mode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? StatusNote { get; set; }
    public long InputTokens { get; set; }
    public long CachedInputTokens { get; set; }
    public long OutputTokens { get; set; }
    public decimal CostUsd { get; set; }
    public long? EstimatedTokens { get; set; }
    public decimal? EstimatedCostUsd { get; set; }
    public long? MaxTokens { get; set; }
    public decimal? MaxCostUsd { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    public int Packages { get; set; }
    public int PackagesCompleted { get; set; }
    public int PackagesFailed { get; set; }
    public int ProposedVerses { get; set; }
    public int FlaggedVerses { get; set; }

    /// <summary>Proposals of the run that still wait for a review (status <c>open</c>).</summary>
    public int OpenProposals { get; set; }
}

/// <summary>Run state used by the agent worker (tables of migrations 0025 and 0028).</summary>
public interface IAgentRunRepository
{
    Task<AgentRunRecord?> GetRunAsync(int runId, CancellationToken token = default);

    Task<IReadOnlyList<AgentRunRecord>> ListRunsAsync(int limit, CancellationToken token = default);

    /// <summary>Sets status and note; <c>running</c> sets the start time once, a finished status (not queued/running) sets the end time.</summary>
    Task SetRunStatusAsync(int runId, string status, string? note, CancellationToken token = default);

    /// <summary>Like <see cref="SetRunStatusAsync"/>, but only while the run is not <c>cancelled</c> (a cancel request wins over the worker's final status). Returns false when it was cancelled.</summary>
    Task<bool> SetRunStatusUnlessCancelledAsync(int runId, string status, string? note, CancellationToken token = default);

    Task<string?> GetRunStatusAsync(int runId, CancellationToken token = default);

    /// <summary>Adds the usage of one call to the run totals and to the package.</summary>
    Task AddUsageAsync(int runId, Guid packageId, long inputTokens, long cachedInputTokens, long outputTokens, decimal costUsd, CancellationToken token = default);

    Task SetCapsAsync(int runId, long? maxTokens, decimal? maxCostUsd, CancellationToken token = default);

    /// <summary>Verses (BBCCCVVV) the run already has a proposal for (any status): resume does not ask for them again.</summary>
    Task<IReadOnlySet<int>> GetProposedRefIdsAsync(int runId, CancellationToken token = default);

    /// <summary>The user of a model (<c>ai:&lt;model&gt;</c>, role <c>ai</c>, no password), created on first use.</summary>
    Task<int> GetOrCreateAiUserAsync(string model, CancellationToken token = default);

    /// <summary>Marks runs that were <c>queued</c>/<c>running</c> when the process stopped as <c>failed</c> (resumable). Returns how many.</summary>
    Task<int> MarkInterruptedRunsAsync(string note, CancellationToken token = default);
}
