namespace UnshackledWord.Domain.WebApi.Maintainer;

/// <summary>The running or last vocab lexicon generation (<c>GET api/maintainer/vocab/status</c>); null when none ran since the API started.</summary>
public sealed class VocabGenerateStatusResponse
{
    /// <summary>Operation name of vocab jobs in <see cref="SeedJobResponse.Operation"/>.</summary>
    public const string Operation = "vocabGenerate";

    public SeedJobResponse? Job { get; set; }
}
