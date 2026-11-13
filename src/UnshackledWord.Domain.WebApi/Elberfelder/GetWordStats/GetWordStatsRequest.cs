namespace UnshackledWord.Domain.WebApi.Elberfelder.GetWordStats;

public sealed class GetWordStatsRequest
{
    public const int MaxWordLength = 50;

    /// <summary>A single German word; lower-cased by the server.</summary>
    public string Word { get; set; } = string.Empty;

    /// <summary>"all", "ot" or "nt" (case-insensitive).</summary>
    public string Testament { get; set; } = "all";

    /// <summary>Optional part-of-speech filter (case-insensitive) on the resolved lemma, e.g. "VERB"; without it all PoS are summed. Snapshot only.</summary>
    public string? Pos { get; set; }

    public const int MaxPosLength = 20;
}
