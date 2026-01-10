namespace UnshackledWord.Tooling.SeedDb.Services.StepBible.Models;

public class StepStrongsBaseEntry
{
    public string ExtendedStrongs { get; set; } = default!;
    public string DisambiguatedStrongs { get; set; } = default!;
    public string UnifiedStrongs { get; set; } = default!;
    public string OriginalWord { get; set; } = default!;
    public string Transliteration { get; set; } = default!;
    public string Morphology { get; set; } = default!;
    public string Gloss { get; set; } = default!;

}
