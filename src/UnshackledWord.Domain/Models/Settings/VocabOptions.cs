namespace UnshackledWord.Domain.Models.Settings;

/// <summary>Vocab trainer settings (config section <c>Vocab</c>, roadmap 0022).</summary>
public sealed class VocabOptions
{
    public const string SectionName = "Vocab";

    /// <summary>Minimum share (percent of the entry's head links, function words dropped) for a derived meaning.</summary>
    public decimal MeaningMinShare { get; set; } = 5m;

    /// <summary>Minimum number of head links for a derived meaning.</summary>
    public int MeaningMinCount { get; set; } = 2;

    /// <summary>Maximum number of derived meanings per entry.</summary>
    public int MeaningMax { get; set; } = 6;
}
