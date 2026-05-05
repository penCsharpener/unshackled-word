namespace UnshackledWord.Tooling.SeedDb.Services.ElbMorphology;

public class Number : SmartEnum<Number>
{
    public static readonly Number Singular = new(1, ["Sing", "S"], "Singular", "Singular");
    public static readonly Number Plural = new(2, ["Plur", "P"], "Plural", "Plural");

    private Number(int id, string[] abbreviation, string english, string german)
        : base(id, abbreviation, english, german) { }
}
