namespace UnshackledWord.Tooling.AiWorker.Models;

internal class InternalVerseDto
{
    public int Id { get; set; }
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public string Word { get; set; } = default!;
    public string? Strongs { get; set; }
    public int PositionInVerse { get; set; }
    public int PositionInWord { get; set; }
}
