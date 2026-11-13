namespace UnshackledWord.Domain.WebApi.Maintainer;

/// <summary>Query of <c>POST api/maintainer/exportSeed</c>: which seed to build; the response is the zip itself.</summary>
public sealed class ExportSeedRequest
{
    public const string All = "all";
    public const string Morphology = "morphology";
    public const string Mappings = "mappings";

    /// <summary><c>all</c> (default), <c>morphology</c> or <c>mappings</c>.</summary>
    public string Kind { get; set; } = All;
}
