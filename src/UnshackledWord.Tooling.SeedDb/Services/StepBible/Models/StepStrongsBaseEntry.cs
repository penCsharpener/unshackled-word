using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible.Models;

public class StepStrongsBaseEntry
{
    public StrongsLanguage LanguageId { get; set; }
    public int Number { get; set; }
    public string? Extra { get; set; }
    public string? DisambiguatedExtra { get; set; }
    public string OriginalWord { get; set; } = default!;
    public string OriginalWordNoDiacritics { get; set; } = default!;
    public string Transliteration { get; set; } = default!;
    public string Morphology { get; set; } = default!;
    public string Gloss { get; set; } = default!;
    public List<StepStrongsUnifiedEntry> UnifiedEntries { get; set; } = [];

}
