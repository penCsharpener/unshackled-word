namespace UnshackledWord.Tooling.SeedDb.Services.ElbMorphology;

public class Mood : SmartEnum<Mood>
{
    public static readonly Mood Imperative = new(100000, ["Imp"], "Imperative", "Imperativ");
    public static readonly Mood Indicative = new(200000, ["Ind"], "Indicative", "Indikativ");
    public static readonly Mood Subjunctive = new(300000, ["Sub"], "Subjunctive", "Konjunktiv");

    private Mood(int id, string[] abbreviation, string english, string german)
        : base(id, abbreviation, english, german) { }
}
