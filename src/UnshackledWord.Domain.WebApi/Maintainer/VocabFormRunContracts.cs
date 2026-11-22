namespace UnshackledWord.Domain.WebApi.Maintainer;

/// <summary>Range of a dictionary-form agent run (roadmap 0022.13): the entries of a course in a frequency-rank range whose form is missing or generated.</summary>
public class VocabFormRunRangeRequest
{
    /// <summary>Course code, e.g. <c>nt-greek</c> or <c>ot-hebrew</c>.</summary>
    public string Course { get; set; } = string.Empty;

    /// <summary>First frequency rank (1 = most frequent).</summary>
    public int FromRank { get; set; } = 1;

    /// <summary>Last frequency rank; omitted = to the end of the course (the limit still applies).</summary>
    public int? ToRank { get; set; }

    /// <summary>At most this many entries (default 200, at most 2000); the run's rank range ends at the last of them.</summary>
    public int Limit { get; set; } = 200;

    /// <summary>Profile name of <c>AgentMapping:Profiles</c>.</summary>
    public string Profile { get; set; } = string.Empty;

    /// <summary>Model tag; omitted = the profile's model.</summary>
    public string? Model { get; set; }
}

public sealed class StartVocabFormRunRequest : VocabFormRunRangeRequest
{
    public long? MaxTokens { get; set; }
    public decimal? MaxCostUsd { get; set; }
}

public sealed class VocabFormRunEstimateResponse
{
    public string Course { get; set; } = string.Empty;
    public string Profile { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;

    /// <summary>Entries the run would ask (form missing or generated, not retired).</summary>
    public int Entries { get; set; }

    public int Batches { get; set; }
    public int FromRank { get; set; }
    public int ToRank { get; set; }
    public long EstimatedInputTokens { get; set; }
    public long EstimatedOutputTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public List<string> Warnings { get; set; } = [];
}
