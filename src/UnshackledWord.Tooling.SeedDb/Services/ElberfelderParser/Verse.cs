namespace UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;

public class Verse
{
    public int VerseNumber { get; set; }
    public List<BibleElement> Elements { get; set; } = new();
    public string Text { get; set; }
}
