namespace UnshackledWord.Application.Features.AgentMapping;

public sealed class AgentRunInsert
{
    public int CreatedByUserId { get; init; }

    /// <summary>The AI user the proposals are attributed to; until AI users exist (0016.6) the caller's own id.</summary>
    public int AiUserId { get; init; }

    public string Profile { get; init; } = "manual";
    public string Model { get; init; } = "manual";
    public string PromptVersion { get; init; } = "0";
    public int BibleBookId { get; init; }
    public int FromRefId { get; init; }
    public int ToRefId { get; init; }
    public string Mode { get; init; } = AgentModes.Redo;
    public string Status { get; init; } = "completed";
    public string? StatusNote { get; init; }
    public long? EstimatedTokens { get; init; }
    public decimal? EstimatedCostUsd { get; init; }
    public long? MaxTokens { get; init; }
    public decimal? MaxCostUsd { get; init; }

    /// <summary>MCP token that built the packages of this run (null for the batch worker and the debug endpoint).</summary>
    public int? ApiTokenId { get; init; }
}

public sealed class AgentPackageInsert
{
    public Guid Id { get; init; }
    public int RunId { get; init; }
    public int FromRefId { get; init; }
    public int ToRefId { get; init; }
    public string WordIdsHash { get; init; } = string.Empty;
    public string LocalIdMapJson { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
}

public sealed class AgentPackageRecord
{
    public Guid Id { get; set; }
    public int RunId { get; set; }
    public string WordIdsHash { get; set; } = string.Empty;
    public string LocalIdMap { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public static class AgentPackageStatuses
{
    public const string Open = "open";
    public const string Submitted = "submitted";
    public const string Completed = "completed";
    public const string Stale = "stale";
    public const string Expired = "expired";
    public const string Failed = "failed";
}

/// <summary>A validated verse ready for <c>AgentVerseProposals</c> (real database ids, JSON columns already serialized).</summary>
public sealed class AgentProposalInsert
{
    public int BibleBookId { get; init; }
    public int Chapter { get; init; }
    public int Verse { get; init; }
    public string Language { get; init; } = string.Empty;
    public string LinksJson { get; init; } = "[]";
    public string? LemmasJson { get; init; }
    public string? PartsJson { get; init; }

    /// <summary>Run mode <c>lemma</c>: role/morpheme/part-lemma changes (roadmap 0018).</summary>
    public string? RolesJson { get; init; }

    public string? FlagsJson { get; init; }
    public string? Note { get; init; }
}

public sealed class AgentProposalStoreResult
{
    /// <summary>Id of the new proposal per verse (same order as the input).</summary>
    public IReadOnlyList<long> ProposalIds { get; init; } = [];

    /// <summary>Open proposals of the same verses that were marked superseded.</summary>
    public int Superseded { get; init; }
}

/// <summary>Runs, packages and proposals (implemented in Infrastructure).</summary>
public interface IAgentStoreRepository
{
    Task<int> CreateRunAsync(AgentRunInsert run, CancellationToken token = default);

    Task InsertPackageAsync(AgentPackageInsert package, CancellationToken token = default);

    Task<AgentPackageRecord?> GetPackageAsync(Guid packageId, CancellationToken token = default);

    Task SetPackageStatusAsync(Guid packageId, string status, bool countAttempt, CancellationToken token = default);

    /// <summary>Number of distinct verses the package has a proposal for (any status); a package is completed when this reaches its verse count.</summary>
    Task<int> CountProposedVersesAsync(Guid packageId, CancellationToken token = default);

    /// <summary>Stores the proposals in one transaction; an older open proposal of the same verse becomes <c>superseded</c> first.</summary>
    Task<AgentProposalStoreResult> StoreProposalsAsync(int runId, Guid packageId, IReadOnlyList<AgentProposalInsert> proposals, CancellationToken token = default);
}
