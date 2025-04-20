using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Tooling.SeedDb.Services;

public record WordInfo(
    BibleBook Book,
    string WordInContext,
    string Koine,
    string Lemma,
    string Strongs,
    string PartOfSpeech,
    string ConjugationKey)
{
}
