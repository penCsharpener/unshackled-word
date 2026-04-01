namespace UnshackledWord.Tooling.AiWorker.Models;

file sealed class BibleReference
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public int RefId { get; set; }

    public override string ToString()
    {
        return $"{BookId} {Chapter}:{Verse}";
    }

    public string ToString(string bookName)
    {
        return $"{bookName} {Chapter}:{Verse}";
    }
}
