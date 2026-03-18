namespace UnshackledWord.Domain.Models.BibleStructure;

public sealed class BibleVerse
{
    public BibleBook Book { get; set; }
    public BibleChapter Chapter { get; set; } = default!;
    public int Verse { get; set; }
}
