namespace UnshackledWord.Domain.Models.Study;

public sealed class LexiconEntry
{
    public int LanguageId { get; init; }
    public int Number { get; init; }
    public string? Extra { get; init; }
    public string? DisambiguatedExtra { get; init; }
    public string OriginalWord { get; init; } = default!;
    public string Transliteration { get; init; } = default!;
    public string Morphology { get; init; } = default!;
    public string Gloss { get; init; } = default!;
    public string? Lexicon { get; init; }
}
