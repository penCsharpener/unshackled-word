namespace UnshackledWord.Tooling.SeedDb.Services.ElbMorphology;

public class Case : SmartEnum<Case>
{
    public static readonly Case Accusative = new(1, ["Acc"], "Accusative", "Akkusativ");
    public static readonly Case Dative = new(2, ["Dat"], "Dative", "Dativ");
    public static readonly Case Genitive = new(3, ["Gen"], "Genitive", "Genitiv");
    public static readonly Case Nominative = new(4, ["Nom"], "Nominative", "Nominativ");

    private Case(int id, string[] abbreviation, string english, string german)
        : base(id, abbreviation, english, german) { }
}
