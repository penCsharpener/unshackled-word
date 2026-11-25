namespace UnshackledWord.Application.Features.VerseSimilarity;

/// <summary>
/// Default rebuild parameters (config section <c>VerseSimilarity</c>, roadmap 0027). A maintainer rebuild or the SeedDb verb may override them
/// per run; the values used are stored on the run row.
/// </summary>
public sealed class VerseSimilarityOptions
{
    public const string SectionName = "VerseSimilarity";

    /// <summary>A Strong's in more than this share of the testament's verses is a stop word.</summary>
    public double StopDfShare { get; set; } = VerseSimilarityParameters.Default.StopDfShare;

    /// <summary>Hits kept per source verse.</summary>
    public int TopK { get; set; } = VerseSimilarityParameters.Default.TopK;

    /// <summary>Lowest score kept.</summary>
    public double MinScore { get; set; } = VerseSimilarityParameters.Default.MinScore;

    /// <summary>Weight of the bigram (phrase) bonus.</summary>
    public double PhraseWeight { get; set; } = VerseSimilarityParameters.Default.PhraseWeight;

    public VerseSimilarityParameters ToParameters() => new(StopDfShare, TopK, MinScore, PhraseWeight);
}
