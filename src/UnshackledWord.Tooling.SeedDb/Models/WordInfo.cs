using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Tooling.SeedDb.Services.StatisticalRestorationGnt;

namespace UnshackledWord.Tooling.SeedDb.Models;

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
