namespace UnshackledWord.Tooling.AiWorker.Models;

public sealed class ElbVerseData
{
    public int Id { get; set; }
    public string WordInContext { get; set; } = default!;
    public int PositionInVerse { get; set; }
}