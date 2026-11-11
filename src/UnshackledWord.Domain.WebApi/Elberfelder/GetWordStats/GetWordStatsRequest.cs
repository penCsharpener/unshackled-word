namespace UnshackledWord.Domain.WebApi.Elberfelder.GetWordStats;

public sealed class GetWordStatsRequest
{
    public const int MaxWordLength = 50;

    /// <summary>A single German word; lower-cased by the server.</summary>
    public string Word { get; set; } = string.Empty;

    /// <summary>"all", "ot" or "nt" (case-insensitive).</summary>
    public string Testament { get; set; } = "all";
}
