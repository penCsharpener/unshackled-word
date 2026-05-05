namespace UnshackledWord.Tooling.SeedDb.Services.ElbMorphology;

public class Gender : SmartEnum<Gender>
{
    public static readonly Gender Feminine = new(1, ["Fem"], "Feminine", "Femininum");
    public static readonly Gender Masculine = new(2, ["Masc"], "Masculine", "Maskulinum");
    public static readonly Gender Neuter = new(3, ["Neut"], "Neuter", "Neutrum");

    private Gender(int id, string[] abbreviation, string english, string german)
        : base(id, abbreviation, english, german) { }
}
