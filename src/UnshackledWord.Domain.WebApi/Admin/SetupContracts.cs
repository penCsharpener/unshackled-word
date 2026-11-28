using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Domain.WebApi.Admin;

// Response of GET api/admin/setup (roadmap 0031.5, 04-setup-page.md).

public static class SetupGroups
{
    public const string Base = "base";
    public const string Step = "step";
    public const string Other = "other";
    public const string Derived = "derived";

    public static readonly string[] All = [Base, Step, Other, Derived];
}

public static class SetupSources
{
    public const string Migration = "migration";
    public const string Assets = "assets";
    public const string Download = "download";
}

/// <summary>Status of a step, derived from row counts (03-seed-catalog.md, status rule).</summary>
public static class SetupStepStatuses
{
    public const string Green = "green";
    public const string Amber = "amber";
    public const string Red = "red";
    public const string Grey = "grey";
}

/// <summary>Valid green state with 0 rows (the step ran, the seed had nothing to import).</summary>
public static class SetupNotes
{
    public const string NothingToImport = "nothingToImport";
}

/// <summary>Operation value of the seed job that runs setup steps (0031.6).</summary>
public static class SetupOperation
{
    public const string Name = "setup";
}

public sealed class SetupTableResponse
{
    public string Name { get; set; } = string.Empty;
    public long Rows { get; set; }

    /// <summary>Only for materialized views: false means never filled (Rows is then 0). Omitted for tables.</summary>
    public bool? Populated { get; set; }
}

public sealed class SetupStepResponse
{
    public string Key { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;

    /// <summary>Download host for the "needs internet" hint; download steps only.</summary>
    public string? SourceHost { get; set; }

    public string Status { get; set; } = string.Empty;
    public List<string> Requires { get; set; } = [];

    /// <summary>Requires that are not green; non-empty exactly when Status is grey.</summary>
    public List<string> Missing { get; set; } = [];

    public bool Resumable { get; set; }

    /// <summary>False while the runner has not moved into UnshackledWord.Seeding (0031.5 to 0031.8).</summary>
    public bool Wired { get; set; }

    /// <summary>False for read-only rows (books from migration 0001).</summary>
    public bool Runnable { get; set; }

    /// <summary>The step is idempotent and may be run again by key while green (only stats-views). Omitted when false.</summary>
    public bool? Rerunnable { get; set; }

    /// <summary>A job for this step failed in this API process (makes the step amber). Omitted when false.</summary>
    public bool? Failed { get; set; }

    public string? Note { get; set; }
    public List<SetupTableResponse> Tables { get; set; } = [];
}

public sealed class SetupStatusResponse
{
    public DateTimeOffset CheckedAt { get; set; }
    public List<SetupStepResponse> Steps { get; set; } = [];

    /// <summary>Key of the step the running setup job works on right now; omitted when no setup job runs.</summary>
    public string? CurrentStep { get; set; }

    /// <summary>The running or last setup job of this process (same shape as api/maintainer/seed/status), null when none.</summary>
    public SeedJobResponse? Job { get; set; }
}

/// <summary>
/// Body of <c>POST api/admin/setup/run</c>: exactly one of <see cref="Keys"/>, <see cref="Group"/> or <see cref="All"/>.
/// Groups and "all" run the red wired steps in catalog order; <see cref="Keys"/> names steps explicitly.
/// </summary>
public sealed class SetupRunRequest
{
    public List<string>? Keys { get; set; }
    public string? Group { get; set; }
    public bool All { get; set; }
}
