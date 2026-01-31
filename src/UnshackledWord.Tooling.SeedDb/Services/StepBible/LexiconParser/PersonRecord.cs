using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible.LexiconParser;

public class PersonRecord : ILexiconEntry<BibleEntity>
{
    public BibleEntity Entity { get; set; } = default!;
    public BibleEntity[]? Parents { get; set; }
    public BibleEntity[]? Siblings { get; set; }
    public BibleEntity[]? Partners { get; set; }
    public BibleEntity[]? Offspring { get; set; }
    public string? Tribe { get; set; }
    public string? Note { get; set; }
    public string? Gender { get; set; }
    public string? OriginalSpelling { get; set; }
    public BibleReference[] References { get; set; } = default!;

    public string StepBibleLink { get; set; } = default!;

    public string? Briefest { get; set; }
    public string Brief { get; set; } = default!;
    public string Short { get; set; } = default!;
    public string Article { get; set; } = default!;

    public override string ToString()
    {
        return $"{Entity.Name} | {Entity.FirstOccurance}";
    }
}
