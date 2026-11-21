namespace UnshackledWord.Domain.WebApi.Maintainer;

/// <summary>The running or last refresh of the vocab renderings view (<c>GET api/maintainer/vocab/renderings/status</c>).</summary>
public sealed class VocabRenderingsStatusResponse
{
    /// <summary>Operation name of renderings refresh jobs in <see cref="SeedJobResponse.Operation"/>.</summary>
    public const string Operation = "vocabRenderings";

    /// <summary>Null when the last job was not a renderings refresh or none ran since the API started.</summary>
    public SeedJobResponse? Job { get; set; }

    /// <summary>Last successful refresh of <c>StatsVocabRenderings</c>; null = never refreshed (the view has no data).</summary>
    public DateTime? RefreshedAt { get; set; }
}
