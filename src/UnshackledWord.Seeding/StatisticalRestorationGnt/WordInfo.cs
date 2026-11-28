using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Seeding.StatisticalRestorationGnt;

namespace UnshackledWord.Seeding.StatisticalRestorationGnt;

public record WordInfo(
    BibleBook Book,
    BibleReference BibRef,
    int PositionInVerse,
    string WordInContext,
    string Koine,
    string Lemma,
    string Strongs,
    string PartOfSpeech,
    string ConjugationKey,
    SrTsvParserStrategy.GrammaticalKey GrammaticalKey)
{
}
