namespace UnshackledWord.Domain.Models.Study;

/// <summary>Rules on top of the mapping stats snapshot that are evaluated at read time (changing them needs no refresh).</summary>
public static class StatsRules
{
    /// <summary>A German lemma (summed over its PoS) with at least this many occurrences is "high frequency" (final, maintainer decision 2026-11-12).</summary>
    public const int HighFrequencyThreshold = 2000;

    public static bool IsHighFrequency(int lemmaOccurrences) => lemmaOccurrences >= HighFrequencyThreshold;
}
