namespace UnshackledWord.Tooling.SeedDb.Services.ElbMorphology;

public class AdjectiveDegree : SmartEnum<AdjectiveDegree>
{
    public static readonly AdjectiveDegree Positive = new(1, ["Pos"], "Positive", "Positiv");
    public static readonly AdjectiveDegree Comparative = new(2, ["Cmp"], "Comparative", "Komparativ");
    public static readonly AdjectiveDegree Superlative = new(3, ["Sup"], "Superlative", "Superlativ");

    public AdjectiveDegree(int id, string[] abbreviation, string english, string german) : base(id, abbreviation, english, german)
    {
    }
}
