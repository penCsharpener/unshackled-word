using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;

namespace UnshackledWord.Application.Features.VerseMapping;

public static class VerseOriginalWordExtensions
{
    /// <summary>The original word as the mapping editor receives it; empty STEP fields become null (omitted from the JSON).</summary>
    public static VerseMappingOriginalWord ToResponse(this VerseOriginalWordRow word) => new()
    {
        Id = word.Id,
        Position = word.Position,
        Original = word.Original,
        Transliteration = NullIfEmpty(word.Transliteration),
        Lemma = NullIfEmpty(word.Lemma),
        Strongs = NullIfEmpty(word.Strongs),
        Gloss = NullIfEmpty(word.Gloss),
        MorphologyCode = NullIfEmpty(word.MorphologyCode),
    };

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
