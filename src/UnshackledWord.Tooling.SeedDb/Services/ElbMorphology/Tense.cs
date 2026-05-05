namespace UnshackledWord.Tooling.SeedDb.Services.ElbMorphology;

public class Tense : SmartEnum<Tense>
{
    public static readonly Tense Present = new(1, "Pres", "Present", "Präsens");
    public static readonly Tense Past = new(2, "Past", "Past", "Präteritum");
    public static readonly Tense Future = new(3, "Fut", "Future", "Futur");
    public static readonly Tense Pluperfect = new(4, "Pqp", "Pluperfect", "Plusquamperfekt");

    private Tense(int id, string abbreviation, string english, string german)
        : base(id, [abbreviation], english, german) { }
}
