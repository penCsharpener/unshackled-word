using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Tooling.SeedDb.Models;

public record WordInfo(
    BibleBook Book,
    int BibleBookId,
    int Chapter,
    int Verse,
    string WordInContext,
    string Koine,
    string Lemma,
    string Strongs,
    string PartOfSpeech,
    string ConjugationKey)
{
}
