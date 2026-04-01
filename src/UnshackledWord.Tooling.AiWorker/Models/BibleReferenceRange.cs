namespace UnshackledWord.Tooling.AiWorker.Models;

file sealed class BibleReferenceRange
{
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int MinVerse { get; set; }
    public int MaxVerse { get; set; }

    public override string ToString()
    {
        return $"{BibleBookId} {Chapter}:{MinVerse}-{MaxVerse}";
    }
}
