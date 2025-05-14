namespace UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;

public class XmlBibleBook
{
    public int BookNumber { get; set; }
    public string BookName { get; set; }
    public string ShortName { get; set; }
    public List<Chapter> Chapters { get; set; } = new();
}
