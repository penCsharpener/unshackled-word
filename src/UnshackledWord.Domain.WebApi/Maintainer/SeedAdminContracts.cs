namespace UnshackledWord.Domain.WebApi.Maintainer;

/// <summary>Why a seed step did nothing; the web UI translates these codes.</summary>
public static class SeedSkipReason
{
    public const string NoSeed = "no-seed";
    public const string TablesNotEmpty = "tables-not-empty";
    public const string UpdateDisabled = "update-disabled";
    public const string NoBaseline = "no-baseline";
    public const string UpToDate = "up-to-date";
    public const string StateEmpty = "state-empty";
    public const string NoWords = "no-words";
    public const string UnsupportedFormat = "unsupported-format";
    public const string UpdateUnsupported = "update-unsupported";
}

/// <summary>Why a rebase was refused before anything was changed.</summary>
public static class RebaseRefusal
{
    public const string BackupNotConfirmed = "backup-not-confirmed";
    public const string BaseTextMissing = "base-text-missing";
    public const string MappingV2 = "mapping-v2";
    public const string EmptyDatabase = "empty-database";
}

/// <summary>Operations of <c>POST api/maintainer/seed/jobs</c>.</summary>
public static class SeedOperations
{
    public const string Export = "export";
    public const string Import = "import";
    public const string Update = "update";
    public const string Rebase = "rebase";
    public const string RoundTrip = "roundTrip";

    public static readonly string[] All = [Export, Import, Update, Rebase, RoundTrip];
}

/// <summary>A capped list of what a step decided (replaced / kept / new / removed variants or verses, orphans).</summary>
public sealed class SeedReportList
{
    public int Total { get; set; }
    public List<string> Items { get; set; } = [];
}

/// <summary>
/// What one seed step (import or update of one kind) did, in numbers and capped lists. The importers still write their CSV reports
/// below <c>temp/seed-report</c>; this is the same information without opening the files.
/// </summary>
public sealed class SeedStepReport
{
    public const int MaxListItems = 200;

    public string Kind { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public bool Skipped { get; set; }
    public string? Reason { get; set; }
    public string? SeedVersion { get; set; }
    public Dictionary<string, int> Counts { get; set; } = [];
    public Dictionary<string, SeedReportList> Lists { get; set; } = [];
    public List<string> ReportFiles { get; set; } = [];

    public static SeedStepReport Skip(string kind, string action, string reason, string? seedVersion = null) => new()
    {
        Kind = kind,
        Action = action,
        Skipped = true,
        Reason = reason,
        SeedVersion = seedVersion
    };

    public SeedStepReport WithCount(string name, int value)
    {
        Counts[name] = value;

        return this;
    }

    public SeedStepReport WithReportFile(string fileName)
    {
        ReportFiles.Add(fileName);

        return this;
    }

    public SeedStepReport AddList<T>(string name, IReadOnlyCollection<T> items, Func<T, string> format)
    {
        Lists[name] = new SeedReportList { Total = items.Count, Items = items.Take(MaxListItems).Select(format).ToList() };

        return this;
    }
}

/// <summary>Files of two seed trees compared by SHA-256; capped lists plus the real counts.</summary>
public sealed class SeedFilesDiff
{
    public int Compared { get; set; }
    public int Identical { get; set; }
    public SeedReportList Changed { get; set; } = new();
    public SeedReportList OnlyInNew { get; set; } = new();
    public SeedReportList OnlyInOld { get; set; } = new();

    public bool IsEmpty => Changed.Total == 0 && OnlyInNew.Total == 0 && OnlyInOld.Total == 0;
}

public sealed class SeedDiskExportResult
{
    public string Kind { get; set; } = string.Empty;
    public string SeedRoot { get; set; } = string.Empty;
    public int Files { get; set; }

    /// <summary>What the export changed on disk: <c>OnlyInNew</c> = created, <c>Changed</c> = rewritten with other content, <c>OnlyInOld</c> = removed.</summary>
    public SeedFilesDiff Diff { get; set; } = new();
}

public sealed class SeedRoundTripResult
{
    public string Kind { get; set; } = string.Empty;
    public int Files { get; set; }

    /// <summary>Two exports in a row (nothing changed in between) produced byte-identical files.</summary>
    public bool SecondExportIdentical { get; set; }

    public SeedFilesDiff SecondExport { get; set; } = new();

    /// <summary>The export equals the files of the repo seed on disk.</summary>
    public bool MatchesDisk { get; set; }

    /// <summary>New = the export, old = the files on disk.</summary>
    public SeedFilesDiff Disk { get; set; } = new();
}

public sealed class SeedRebaseCounts
{
    public int Words { get; set; }
    public int Morphology { get; set; }
    public int Mappings { get; set; }
}

public sealed class SeedRebaseResult
{
    /// <summary>Set when nothing was changed because a guard refused; one of <see cref="RebaseRefusal"/>.</summary>
    public string? Refused { get; set; }

    public SeedRebaseCounts? Before { get; set; }
    public SeedRebaseCounts? After { get; set; }
    public int RevisionRealigned { get; set; }
    public int RevisionMissing { get; set; }
    public List<SeedStepReport> Steps { get; set; } = [];
}

/// <summary>Result of a finished job; only the part that belongs to the operation is set.</summary>
public sealed class SeedJobResult
{
    public SeedDiskExportResult? Export { get; set; }
    public List<SeedStepReport> Steps { get; set; } = [];
    public SeedRebaseResult? Rebase { get; set; }
    public SeedRoundTripResult? RoundTrip { get; set; }
}

public sealed class SeedJobResponse
{
    public const string Running = "running";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";

    public string Id { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Status { get; set; } = Running;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public List<string> Log { get; set; } = [];
    public string? Error { get; set; }
    public SeedJobResult? Result { get; set; }
}

/// <summary>Body of <c>POST api/maintainer/seed/jobs</c>.</summary>
public sealed class StartSeedJobRequest
{
    /// <summary><see cref="SeedOperations"/>: export, import, update, rebase or roundTrip.</summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary><c>all</c>, <c>morphology</c>, <c>mappings</c> or <c>revision</c>. Rebase always handles all kinds.</summary>
    public string Kind { get; set; } = ExportSeedRequest.Revision;

    /// <summary>Required (true) for rebase: the maintainer confirms that a database backup exists.</summary>
    public bool ConfirmBackup { get; set; }
}

public sealed class SeedImportRowResponse
{
    public int Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string SeedVersion { get; set; } = string.Empty;
    public DateTimeOffset? ExportedAt { get; set; }
    public DateTimeOffset ImportedAt { get; set; }
    public int Rows { get; set; }
    public int Exact { get; set; }
    public int Shifted { get; set; }
    public int Modified { get; set; }
    public int Orphaned { get; set; }
    public int? VersesUpdated { get; set; }
    public int? VersesKeptLocal { get; set; }
}

/// <summary>Answer of <c>GET api/maintainer/seed/status</c>: the running or last job, whether the repo seed folder is reachable, and the latest <c>SeedImports</c> rows.</summary>
public sealed class SeedStatusResponse
{
    /// <summary>Null when no job ran since the API started.</summary>
    public SeedJobResponse? Job { get; set; }

    /// <summary>Null when the assets folder is reachable and writable; otherwise why export, import and rebase cannot run from here.</summary>
    public string? AssetsProblem { get; set; }

    public List<SeedImportRowResponse> Imports { get; set; } = [];
}
