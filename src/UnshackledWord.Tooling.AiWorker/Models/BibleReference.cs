namespace UnshackledWord.Tooling.AiWorker.Models;

internal sealed class BibleReference
{
    public int Id { get; set; }
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public int RefId { get; set; }

    public override string ToString()
    {
        return $"{BibleBookId} {Chapter}:{Verse}";
    }

    public string ToString(string bookName)
    {
        return $"{bookName} {Chapter}:{Verse}";
    }
}
