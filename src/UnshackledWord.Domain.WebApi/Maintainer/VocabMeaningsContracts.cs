namespace UnshackledWord.Domain.WebApi.Maintainer;

/// <summary>Body of <c>POST api/maintainer/vocab/meanings/derive</c>. Without a course the scope is all entries.</summary>
public sealed class DeriveVocabMeaningsRequest
{
    /// <summary>Course code (<c>nt-greek</c>, <c>ot-hebrew</c>); null = all entries.</summary>
    public string? Course { get; set; }

    /// <summary>First frequency rank (inclusive) within the course; needs <see cref="Course"/>.</summary>
    public int? RankFrom { get; set; }

    /// <summary>Last frequency rank (inclusive) within the course; needs <see cref="Course"/>.</summary>
    public int? RankTo { get; set; }

    /// <summary>Compute and report only, write nothing.</summary>
    public bool DryRun { get; set; }
}

/// <summary>The running or last meaning derivation (<c>GET api/maintainer/vocab/meanings/status</c>); null when none ran since the API started.</summary>
public sealed class VocabMeaningsStatusResponse
{
    /// <summary>Operation name of meaning derivation jobs in <see cref="SeedJobResponse.Operation"/>.</summary>
    public const string Operation = "vocabMeanings";

    public SeedJobResponse? Job { get; set; }
}
