namespace UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;

public class XmlBible
{
    public string BibleName { get; set; }
    public string Revision { get; set; }
    public string Type { get; set; }
    public BibleInformation Information { get; set; }
    public List<XmlBibleBook> Books { get; set; } = new();
}