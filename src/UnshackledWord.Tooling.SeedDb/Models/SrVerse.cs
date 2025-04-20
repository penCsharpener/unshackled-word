using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Tooling.SeedDb.Models;

public record struct SrVerse(int Id, BibleBook BibleBook, int ChapterId, int VerseId, string Verse)
{
    public override string ToString()
    {
        return $"{Id} {Verse}";
    }
}
