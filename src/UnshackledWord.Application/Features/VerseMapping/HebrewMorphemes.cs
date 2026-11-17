using UnshackledWord.Application.Features.LinkRoles;
using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;

namespace UnshackledWord.Application.Features.VerseMapping;

/// <summary>
/// Morphemes of a Hebrew word from StepHebrewWords.ExpandedStrongTags (<c>H9001=ו=&amp;/{H0559=אָמַר=to say}</c>) or DisambiguatedStrongs
/// (<c>H9001/{H0559}\H9014</c>): pieces split at <c>/</c> and <c>\</c>, the braced piece is the main morpheme, a trailing <c>+</c> is dropped.
/// Punctuation tags H9014–H9019 (maqqef, sof pasuq, paragraph and section marks) are never morphemes (roadmap 0018, 04-roles-and-counting.md).
/// </summary>
public static class HebrewMorphemes
{
    /// <summary>Delegates to <see cref="SourceMorphemes.ParseHebrew"/>, the one Hebrew morpheme parser (drops H9014–H9019, strips <c>#n</c>).</summary>
    public static IReadOnlyList<VerseMappingMorpheme> Parse(string? tags) =>
        SourceMorphemes.ParseHebrew(tags)
            .Select(m => new VerseMappingMorpheme { Strongs = m.Strongs, IsMain = m.IsMain, Original = m.Original, Gloss = m.Gloss })
            .ToList();

    /// <summary>The Strong's tag of the main morpheme, or null when the word has no morphemes.</summary>
    public static string? Main(IReadOnlyList<VerseMappingMorpheme> morphemes) => morphemes.FirstOrDefault(m => m.IsMain)?.Strongs;

    /// <summary>H9014–H9019: maqqef, sof pasuq, paragraph and section marks (<see cref="SourceMorphemes.IsPunctuation"/>).</summary>
    public static bool IsPunctuation(string strongs) => SourceMorphemes.IsPunctuation(strongs);

    /// <summary>The article morpheme ה (H9009), the target of an APPRART contraction like <c>im</c>.</summary>
    public static bool IsArticle(string strongs) => SourceMorphemes.HebrewNumber(strongs) == 9009;
}
