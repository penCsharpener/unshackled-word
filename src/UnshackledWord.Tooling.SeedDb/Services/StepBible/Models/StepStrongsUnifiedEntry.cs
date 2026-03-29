using UnshackledWord.Domain.Models.Dbo.Step;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible.Models;

public class StepStrongsUnifiedEntry
{
    public int Id { get; set; }
    public int StepStrongsLexiconId { get; set; }
    public StrongsLanguage LanguageId { get; set; }
    public int Number { get; set; }
    public string? Extra { get; set; }
}
