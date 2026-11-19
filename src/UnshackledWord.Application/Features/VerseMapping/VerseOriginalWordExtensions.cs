using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;

namespace UnshackledWord.Application.Features.VerseMapping;

public static class VerseOriginalWordExtensions
{
    /// <summary>The original word as the mapping editor receives it; empty STEP fields become null (omitted from the JSON).</summary>
    /// <param name="withMorphemes">Hebrew: fill <see cref="VerseMappingOriginalWord.Morphemes"/> from <see cref="VerseOriginalWordRow.MorphemeTags"/>; also selects the Hebrew morphology decoder.</param>
    public static VerseMappingOriginalWord ToResponse(this VerseOriginalWordRow word, bool withMorphemes = false) => new()
    {
        Id = word.Id,
        Position = word.Position,
        Original = word.Original,
        Transliteration = NullIfEmpty(word.Transliteration),
        Lemma = NullIfEmpty(word.Lemma),
        Strongs = NullIfEmpty(word.Strongs),
        Gloss = NullIfEmpty(word.Gloss),
        MorphologyCode = NullIfEmpty(word.MorphologyCode),
        Morphology = MorphologyResponses.Decode(withMorphemes, word.MorphologyCode),
        Morphemes = withMorphemes ? HebrewMorphemes.Parse(word.MorphemeTags).ToList() : null,
    };

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
