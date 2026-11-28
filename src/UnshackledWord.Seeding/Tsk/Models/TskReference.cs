using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Seeding.Tsk.Models;

public class TskReference
{
    public BibleReference Reference { get; set; } = default!;
    /// <summary>The TSK file's sort column: order of the keyword entries within a verse.</summary>
    public int Sort { get; set; }
    public string Words { get; set; } = default!;
    public ICollection<IBibleReference> CrossReferences { get; set; } = default!;
}
