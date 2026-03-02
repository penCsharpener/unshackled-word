namespace UnshackledWord.Tooling.AiWorker.Models;

internal sealed class BibleReference
{
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
}

internal sealed class BibleReferenceRange
{
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int MinVerse { get; set; }
    public int MaxVerse { get; set; }
}
