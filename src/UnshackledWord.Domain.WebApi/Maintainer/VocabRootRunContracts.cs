namespace UnshackledWord.Domain.WebApi.Maintainer;

/// <summary>Range of a vocab root agent run (roadmap 0030.4): the entries of a course in a frequency-rank range whose main key has no root row.</summary>
public class VocabRootRunRangeRequest
{
    /// <summary>Course code, e.g. <c>nt-greek</c> or <c>ot-hebrew</c>.</summary>
    public string Course { get; set; } = string.Empty;

    /// <summary>First frequency rank (1 = most frequent).</summary>
    public int FromRank { get; set; } = 1;

    /// <summary>Last frequency rank; omitted = to the end of the course (the limit, at most 2000, still applies).</summary>
    public int? ToRank { get; set; }

    /// <summary>At most this many entries (1..2000); omitted = up to 2000, so a from..to range is covered whole in one run. The run's rank range
    /// ends at the last selected entry; estimate and start select the same entries for the same request.</summary>
    public int? Limit { get; set; }

    /// <summary>Entries per model call (1..200); omitted = 25. Stored on the run, so a resumed run keeps it.</summary>
    public int? BatchSize { get; set; }

    /// <summary>Profile name of <c>AgentMapping:Profiles</c>.</summary>
    public string Profile { get; set; } = string.Empty;

    /// <summary>Model tag; omitted = the profile's model.</summary>
    public string? Model { get; set; }

    /// <summary>Option "Vorbefüllung prüfen": entries with a <c>prefill</c> row are asked too and their row is replaced (listed in the run report). Manual rows are never touched.</summary>
    public bool CheckPrefill { get; set; }
}

public sealed class StartVocabRootRunRequest : VocabRootRunRangeRequest
{
    public long? MaxTokens { get; set; }
    public decimal? MaxCostUsd { get; set; }
}

public sealed class VocabRootRunEstimateResponse
{
    public string Course { get; set; } = string.Empty;
    public string Profile { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;

    /// <summary>Entries the run would ask (no root row, plus prefill rows with the check option; morphemes and proper names skipped).</summary>
    public int Entries { get; set; }

    /// <summary>Model calls of the run: <see cref="Entries"/> divided by <see cref="BatchSize"/>, rounded up.</summary>
    public int Batches { get; set; }

    /// <summary>Entries per model call the run uses (the request's value or the default 25).</summary>
    public int BatchSize { get; set; }

    public int FromRank { get; set; }
    public int ToRank { get; set; }
    public long EstimatedInputTokens { get; set; }
    public long EstimatedOutputTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public List<string> Warnings { get; set; } = [];
}

/// <summary>An entry of the run report: <c>rejected</c> (rule broken), <c>flagged</c> (unsure, not written) or <c>replaced</c> (prefill row replaced).</summary>
public sealed class VocabRootRunItem
{
    public int EntryId { get; set; }
    public string Lemma { get; set; } = string.Empty;
    public string StrongsKey { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;

    /// <summary>The model's answer as short text, e.g. <c>σύν + ἐπί + G3140</c>.</summary>
    public string? Answer { get; set; }
}

/// <summary>Run report of a <c>vocab-root</c> run: the entries the maintainer should look at.</summary>
public sealed class VocabRootRunReportResponse
{
    public int RunId { get; set; }
    public int Rejected { get; set; }
    public int Flagged { get; set; }
    public int Replaced { get; set; }
    public List<VocabRootRunItem> Items { get; set; } = [];
}
