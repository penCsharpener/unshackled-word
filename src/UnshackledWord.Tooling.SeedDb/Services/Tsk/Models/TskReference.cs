using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Tooling.SeedDb.Services.Tsk.Models;

public class TskReference
{
    public BibleReference Reference { get; set; } = default!;
    public string Words { get; set; } = default!;
    public ICollection<IBibleReference> CrossReferences { get; set; } = default!;
}
