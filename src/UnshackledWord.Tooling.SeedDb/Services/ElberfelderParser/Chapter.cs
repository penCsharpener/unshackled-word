namespace UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;

public class Chapter
{
    public int ChapterNumber { get; set; }
    public List<Verse> Verses { get; set; } = new();
}
