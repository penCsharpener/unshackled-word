namespace UnshackledWord.Domain.Models.Study;

/// <summary>German word of the Elberfelder 1871 as needed by the word detail.</summary>
public sealed class ElbWordRow
{
    public int Id { get; init; }
    public int BibleBookId { get; init; }
    public int Chapter { get; init; }
    public int Verse { get; init; }
    public int HebRefId { get; init; }
    public int PositionInVerse { get; init; }
    public string WordInContext { get; init; } = default!;
    public string? GermanLemma { get; init; }
}
