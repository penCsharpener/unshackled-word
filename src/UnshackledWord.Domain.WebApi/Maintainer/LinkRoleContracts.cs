namespace UnshackledWord.Domain.WebApi.Maintainer;

/// <summary>Modes of the link-roles backfill job (roadmap 0018.4).</summary>
public static class LinkRoleJobModes
{
    /// <summary>Computes the change set and reports it; writes nothing.</summary>
    public const string DryRun = "dryRun";

    /// <summary>Computes the change set again and writes it as one change set in one transaction.</summary>
    public const string Apply = "apply";

    /// <summary>Restores the before values of the latest applied change set.</summary>
    public const string Undo = "undo";

    public static readonly string[] All = [DryRun, Apply, Undo];
}

/// <summary>POST maintainer/link-roles/jobs.</summary>
public sealed class StartLinkRoleJobRequest
{
    public string Mode { get; set; } = LinkRoleJobModes.DryRun;
}

/// <summary>The running or last link-roles job, polled through GET maintainer/link-roles/status.</summary>
public sealed class LinkRoleJobResponse
{
    public const string Running = "running";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";

    public string Id { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string Status { get; set; } = Running;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public List<string> Log { get; set; } = [];
    public string? Error { get; set; }
    public LinkRoleJobResult? Result { get; set; }
}

/// <summary>Report of a dry run or apply; plus the written change set (apply) or the restored rows (undo).</summary>
public sealed class LinkRoleJobResult
{
    public LinkRoleReport? Report { get; set; }
    public int? ChangeSetId { get; set; }
    public int MappingRows { get; set; }
    public int LemmaRows { get; set; }
    public int? UndoChangeSetId { get; set; }
    public int MappingSkipped { get; set; }
    public int LemmasSkipped { get; set; }
}

/// <summary>
/// What the backfill does. Counts are keyed by language (<c>greek</c>, <c>hebrew</c>, <c>german</c> for lemma rules) and then by rule id
/// (<c>1</c>, <c>1-main</c>, <c>2</c>, <c>3</c>, <c>3b</c>, <c>4</c>, <c>5</c>, <c>6</c>).
/// </summary>
public sealed class LinkRoleReport
{
    public const int SamplesPerRule = 20;

    public int Verses { get; set; }
    public int Links { get; set; }

    /// <summary>Links (and lemmas for 3b/6) a rule decided, whether or not the stored value already matched.</summary>
    public Dictionary<string, Dictionary<string, int>> Decided { get; set; } = [];

    /// <summary>Rows the apply writes, per language and rule (decisions that change nothing are left out).</summary>
    public Dictionary<string, Dictionary<string, int>> Changed { get; set; } = [];

    public int Undecided { get; set; }

    /// <summary>Undecided links per language and reason (numbers and words in the reason replaced, so equal reasons group).</summary>
    public Dictionary<string, Dictionary<string, int>> UndecidedByReason { get; set; } = [];

    public int MappingRows { get; set; }
    public int LemmaRows { get; set; }

    /// <summary>Up to <see cref="SamplesPerRule"/> verses per rule with the changes of that rule, spread over the Bible.</summary>
    public Dictionary<string, List<LinkRoleSample>> Samples { get; set; } = [];
}

public sealed class LinkRoleSample
{
    public int HebRefId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public List<LinkRoleSampleChange> Changes { get; set; } = [];
}

/// <summary>One change: <c>Before</c>/<c>After</c> like <c>head H0559</c>, <c>unit G0907</c>, <c>added → sprach</c>, <c>lemma stehen</c>.</summary>
public sealed class LinkRoleSampleChange
{
    public string Word { get; set; } = string.Empty;
    public string Before { get; set; } = string.Empty;
    public string After { get; set; } = string.Empty;
}

/// <summary>GET maintainer/link-roles/status.</summary>
public sealed class LinkRoleStatusResponse
{
    public LinkRoleJobResponse? Job { get; set; }

    /// <summary>The latest applied change set that was not undone (what "undo" would restore).</summary>
    public LinkRoleChangeSetDto? Applied { get; set; }

    /// <summary><c>DqRefreshState.LinkRolesAppliedAt</c>; null = roles incomplete.</summary>
    public DateTimeOffset? LinkRolesAppliedAt { get; set; }
}

public sealed class LinkRoleChangeSetDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int MappingRows { get; set; }
    public int LemmaRows { get; set; }
}
