using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;

public record ElbVerse(BibleBook BibleBook, int ChapterId, int VerseId, string Text, IList<ElbWord> Words)
{
}
