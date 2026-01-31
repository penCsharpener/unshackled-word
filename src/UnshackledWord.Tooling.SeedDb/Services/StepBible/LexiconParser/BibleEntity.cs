using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible.LexiconParser;

public class BibleEntity
{
    public string Name { get; set; } = default!;
    public BibleReference FirstOccurance { get; set; }
    public string? Strongs { get; set; } = default!;
}