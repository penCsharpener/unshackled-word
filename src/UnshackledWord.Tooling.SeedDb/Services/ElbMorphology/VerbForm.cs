namespace UnshackledWord.Tooling.SeedDb.Services.ElbMorphology;

public class VerbForm : SmartEnum<VerbForm>
{
    public static readonly VerbForm Finite = new(1, "Fin", "Finite Verb", "Finites Verb");
    public static readonly VerbForm Infinitive = new(2, "Inf", "Infinitive", "Infinitiv");
    public static readonly VerbForm Participle = new(3, "Part", "Participle", "Partizip");

    private VerbForm(int id, string abbreviation, string english, string german)
        : base(id, [abbreviation], english, german) { }
}
