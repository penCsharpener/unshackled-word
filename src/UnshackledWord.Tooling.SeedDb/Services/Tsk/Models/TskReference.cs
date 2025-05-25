using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Tooling.SeedDb.Services.Tsk.Models;

public class TskReference
{
    public BibleBook Book { get; set; } = default!;
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public string Words { get; set; } = default!;
    public ICollection<IBibleReference> CrossReferences { get; set; } = default!;
}
