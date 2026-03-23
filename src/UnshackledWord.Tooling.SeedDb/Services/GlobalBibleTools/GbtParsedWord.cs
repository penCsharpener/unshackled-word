using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Tooling.SeedDb.Services.GlobalBibleTools;

public class GbtParsedWord
{
    public BibleReference BibleReference { get; set; }
    public int PositionInVerse { get; set; }
    public string Text { get; set; } = null!;
    public string GrammarKey { get; set; } = null!;
}
