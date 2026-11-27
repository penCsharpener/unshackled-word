namespace UnshackledWord.Domain.WebApi.Maintainer;

/// <summary>The running or last root prefill plus the root counts (<c>GET api/maintainer/vocab/roots/status</c>).</summary>
public sealed class VocabRootStatusResponse
{
    /// <summary>Operation name of root prefill jobs in <see cref="SeedJobResponse.Operation"/>.</summary>
    public const string Operation = "vocabRootPrefill";

    /// <summary>Null when the last job was not a root prefill or none ran since the API started.</summary>
    public SeedJobResponse? Job { get; set; }

    public VocabRootCountsResponse Counts { get; set; } = new();
}

/// <summary>Active vocab entries by the origin of the root of their main key (roadmap 0030.2).</summary>
public sealed class VocabRootCountsResponse
{
    public int Prefill { get; set; }

    /// <summary>Roots written by the AI run (shown with the KI badge).</summary>
    public int Agent { get; set; }

    public int Manual { get; set; }

    /// <summary>"Ohne Wurzel": active entries that are neither morpheme nor proper name and whose main key has no root row.</summary>
    public int Missing { get; set; }

    /// <summary>"Nicht zutreffend": active morphemes and proper names, which get no root.</summary>
    public int NotApplicable { get; set; }
}
