namespace UnshackledWord.Domain.Models;

public class ElberfelderVerseInfo
{
    public int Id { get; set; }
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public string Text { get; set; } = default!;
}
