namespace UnshackledWord.Tooling.AiWorker.Mapping.Models;

internal sealed class MappingScopeRange
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
