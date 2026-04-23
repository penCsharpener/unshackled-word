namespace UnshackledWord.Domain.Models.Dbo.Step;

public sealed class StepStrongsLexiconDbo : IEntityId
{
    public const string DboName = "\"unshackled-word\".\"StepStrongsLexicon\"";

    public int Id { get; set; }
    public StrongsLanguage LanguageId { get; set; }
    public int Number { get; set; }
    public string? Extra { get; set; }
    public string? DisambiguatedExtra { get; set; }
    public string OriginalWord { get; set; } = default!;
    public string OriginalWordNoDiacritics { get; set; } = default!;
    public string Transliteration { get; set; } = default!;
    public string Morphology { get; set; } = default!;
    public string Gloss { get; set; } = default!;
    public string? Lexicon { get; set; }
    public List<StepUnifiedStrongsDbo> UnifiedStrongs { get; set; } = default!;
}
