using CsvHelper.Configuration;

namespace UnshackledWord.Infrastructure.Seed.Lxx;

/// <summary>
/// One line of <c>assets/seed/lxx/tags.csv</c> (roadmap 0028.8, docs/roadmaps/0028-lxx-rahlfs/04-model.md): one confirmed <c>LxxWordTags</c> row. The LXX word is
/// anchored by Rahlfs reference, text form, position and the hash of the word (<c>LxxWords.SrcHash</c>), never by its Greek text or database id; there is no
/// user id. There is no lemma column: the lemma is Greek script, so the file would carry Greek text; import takes it from TBESG by <see cref="Strongs"/>, which is where the
/// stored lemma comes from. <see cref="Source"/> (<c>nt-form</c>, <c>ai</c>, <c>human</c>) keeps how the tag came about; import falls back to <c>human</c> when it is empty.
/// </summary>
public sealed class LxxTagSeedLine
{
    /// <summary><c>LxxVerses.RahlfsRefId</c> (book·10⁸ + chapter·10⁵ + verse·10² + subverse).</summary>
    public long RahlfsRef { get; set; }

    /// <summary>Empty, <c>og</c> or <c>th</c> (Daniel).</summary>
    public string TextForm { get; set; } = string.Empty;

    public int Position { get; set; }
    public string SrcHash { get; set; } = string.Empty;
    public string Strongs { get; set; } = string.Empty;
    public string Morphology { get; set; } = string.Empty;
    public string? Source { get; set; }
}

public sealed class LxxTagSeedLineMap : ClassMap<LxxTagSeedLine>
{
    public LxxTagSeedLineMap()
    {
        Map(x => x.RahlfsRef).Index(0).Name("RahlfsRef");
        Map(x => x.TextForm).Index(1).Name("TextForm");
        Map(x => x.Position).Index(2).Name("Position");
        Map(x => x.SrcHash).Index(3).Name("SrcHash");
        Map(x => x.Strongs).Index(4).Name("Strongs");
        Map(x => x.Morphology).Index(5).Name("Morphology");
        Map(x => x.Source).Index(6).Name("Source");
    }
}

/// <summary>
/// One line of <c>assets/seed/lxx/links.csv</c>: one confirmed <c>LxxHebrewLinks</c> row. The LXX side is anchored as in <see cref="LxxTagSeedLine"/>, the Hebrew
/// side by the <c>SourceKey</c> of the STEP Hebrew word (<see cref="HebLxxRefId"/> = <c>StepHebrewWords.LxxRefId</c>, position in the verse, hash of the word),
/// because <c>StepHebrewWords.Id</c> is renumbered by a STEP re-import.
/// </summary>
public sealed class LxxLinkSeedLine
{
    public long RahlfsRef { get; set; }
    public string TextForm { get; set; } = string.Empty;
    public int Position { get; set; }
    public string SrcHash { get; set; } = string.Empty;
    public int HebLxxRefId { get; set; }
    public int HebPosition { get; set; }
    public string HebSrcHash { get; set; } = string.Empty;
}

public sealed class LxxLinkSeedLineMap : ClassMap<LxxLinkSeedLine>
{
    public LxxLinkSeedLineMap()
    {
        Map(x => x.RahlfsRef).Index(0).Name("RahlfsRef");
        Map(x => x.TextForm).Index(1).Name("TextForm");
        Map(x => x.Position).Index(2).Name("Position");
        Map(x => x.SrcHash).Index(3).Name("SrcHash");
        Map(x => x.HebLxxRefId).Index(4).Name("HebLxxRefId");
        Map(x => x.HebPosition).Index(5).Name("HebPosition");
        Map(x => x.HebSrcHash).Index(6).Name("HebSrcHash");
    }
}
