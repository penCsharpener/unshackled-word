namespace UnshackledWord.Domain.WebApi.Maintainer;

/// <summary>Tag rows of one (status, source) pair.</summary>
public sealed class LxxTagCountResponse
{
    /// <summary><c>proposed</c> or <c>confirmed</c>.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary><c>nt-form</c>, <c>ai</c> or <c>human</c>.</summary>
    public string Source { get; set; } = string.Empty;

    public int Count { get; set; }
}

/// <summary>Answer of <c>GET api/maintainer/lxx/status</c> (roadmap 0028.6): counts for the Wartung card.</summary>
public sealed class LxxStatusResponse
{
    public int Verses { get; set; }
    public int Words { get; set; }

    /// <summary>Tag rows per status and source; pairs without rows are absent.</summary>
    public List<LxxTagCountResponse> Tags { get; set; } = [];

    /// <summary>True while a prefill job of this process runs.</summary>
    public bool Running { get; set; }

    /// <summary>The running or last prefill job of this API process (log lines with coverage per book); null when none ran since the start.</summary>
    public SeedJobResponse? Job { get; set; }
}

/// <summary>202 answer of <c>POST api/maintainer/lxx/prefill</c>.</summary>
public sealed class StartLxxPrefillResponse
{
    /// <summary>Operation name of the job in <see cref="SeedJobResponse.Operation"/>.</summary>
    public const string Operation = "lxxPrefill";

    public SeedJobResponse Job { get; set; } = new();
}
