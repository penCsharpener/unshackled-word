using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Tooling.SeedDb.Services.GlobalBibleTools;

public class GbtParsedWord
{
    public BibleReference BibleReference { get; set; }
    public int BookId { get; set; }
    public int ChapterId { get; set; }
    public int VerseId { get; set; }
    public int SortNumber { get; set; }
    public string Text { get; set; } = null!;
    public string GrammarKey { get; set; } = null!;
}