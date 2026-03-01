namespace UnshackledWord.Tooling.AiWorker.Models;

public sealed class StepGreekVerseData
{
    public int Id { get; set; }
    public string Greek { get; set; } = default!;
    public string DisambiguatedStrongs { get; set; } = default!;
    public int PositionInVerse { get; set; }
}