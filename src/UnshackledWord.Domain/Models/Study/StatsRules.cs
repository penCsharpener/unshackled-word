namespace UnshackledWord.Domain.Models.Study;

/// <summary>Rules on top of the mapping stats snapshot that are evaluated at read time (changing them needs no refresh).</summary>
public static class StatsRules
{
    /// <summary>A German lemma (summed over its PoS) with at least this many occurrences is "high frequency" (final, maintainer decision 2026-11-12).</summary>
    public const int HighFrequencyThreshold = 2000;

    /// <summary>
    /// An original-language Strong's number (summed over its lexicon variants) with at least this many occurrences is too frequent
    /// to flash TSK links in the reader (καί, ὁ, ἐν, ו …); the cross reference texts leave it out of a verse's keys.
    /// </summary>
    public const int OriginalHighFrequencyThreshold = 1000;

    public static bool IsHighFrequency(int lemmaOccurrences) => lemmaOccurrences >= HighFrequencyThreshold;
}
