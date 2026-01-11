namespace UnshackledWord.Domain.Models.Dbo.Step;

public sealed class StepStrongsDbo : IEntityId
{
    public const string DbName = "\"unshackled-word\".\"StepStrongs\"";

    public int Id { get; set; }
    public string ExtendedStrongs { get; set; } = default!;
    public string DisambiguatedStrongs { get; set; } = default!;
    public string UnifiedStrongs { get; set; } = default!;
    public string OriginalWord { get; set; } = default!;
    public string OriginalWordNoDiacritics { get; set; } = default!;
    public string Transliteration { get; set; } = default!;
    public string Morphology { get; set; } = default!;
    public string Gloss { get; set; } = default!;
    public string? Lexicon { get; set; }
}
