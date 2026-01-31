using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible.LexiconParser;

public sealed class PlaceRecord : ILexiconEntry<BibleEntity>
{
    public BibleEntity Entity { get; set; } = default!;
    public string? GoogleMapsLinks { get; set; }
    public string? PalOpenMapsLink { get; set; }
    public string? Note { get; set; }
    public string? Type { get; set; }
    public string? OriginalSpelling { get; set; }
    public BibleReference[] References { get; set; } = default!;
    public string StepBibleLink { get; set; } = default!;

    public string? Briefest { get; set; }
    public string Brief { get; set; } = default!;
    public string Short { get; set; } = default!;
    public string Article { get; set; } = default!;
}
