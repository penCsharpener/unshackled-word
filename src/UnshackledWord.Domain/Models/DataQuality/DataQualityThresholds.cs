namespace UnshackledWord.Domain.Models.DataQuality;

/// <summary>Fixed thresholds of the data quality checks (not adjustable from the UI).</summary>
public static class DataQualityThresholds
{
    /// <summary>A rendering / Strong's below this share of its group is a minority.</summary>
    public const double MinorityShare = 0.05;

    /// <summary>A rendering with fewer occurrences than this is an outlier candidate regardless of its share (M2).</summary>
    public const int OutlierMaxCount = 3;

    /// <summary>M1: a lemma needs at least this many aligned occurrences.</summary>
    public const int LemmaMinOccurrences = 50;

    /// <summary>M1: a Strong's counts as a real alternative from this share on.</summary>
    public const double LemmaKeyShare = 0.05;

    /// <summary>L3: forms whose non-dominant lemmas stay below this share of the form's occurrences are reported.</summary>
    public const double FormMinorityShare = 0.10;

    /// <summary>L2: a shorter lemma of the same PoS must occur at least this often to count as the probable real lemma.</summary>
    public const int LemmaSetMinCount = 3;

    /// <summary>Heatmap steps (share of unaligned content words) of the colour scale.</summary>
    public static readonly double[] HeatSteps = [0.02, 0.05, 0.10, 0.20];

    /// <summary>C1 tile: chapters from this unaligned share count as findings; from <see cref="CoverageErrorShare"/> as errors.</summary>
    public const double CoverageFindingShare = 0.10;

    public const double CoverageErrorShare = 0.20;

    /// <summary>Words of context left and right of a hit.</summary>
    public const int ContextWords = 5;
}
