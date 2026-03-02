namespace UnshackledWord.Tooling.AiWorker.Models;

public sealed class VerseDataList<T>
{
    public int BookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public IEnumerable<T> Data { get; set; } = default!;
}
