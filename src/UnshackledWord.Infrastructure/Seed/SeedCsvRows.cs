using CsvHelper.Configuration;
using UnshackledWord.Application.Features.Seed.Morphology;

namespace UnshackledWord.Infrastructure.Seed;

/// <summary>Column order and names of <c>morphology/NN-Book.csv</c>: <c>Ref,Pos,Form,Lemma,Upos,Stts,Feats</c>.</summary>
public sealed class MorphologySeedLineMap : ClassMap<MorphologySeedLine>
{
    public MorphologySeedLineMap()
    {
        Map(x => x.Ref).Index(0).Name("Ref");
        Map(x => x.Pos).Index(1).Name("Pos");
        Map(x => x.Form).Index(2).Name("Form");
        Map(x => x.Lemma).Index(3).Name("Lemma");
        Map(x => x.Upos).Index(4).Name("Upos");
        Map(x => x.Stts).Index(5).Name("Stts");
        Map(x => x.Feats).Index(6).Name("Feats");
    }
}

/// <summary>One line of <c>anchors/NN-Book.csv</c>: the fingerprint of a chapter's token list at export time.</summary>
public sealed class AnchorRow
{
    /// <summary>Book and chapter as <c>BBCCC</c> (<c>HebRefId / 1000</c>).</summary>
    public int Chapter { get; set; }

    public int Words { get; set; }

    public string Fingerprint { get; set; } = string.Empty;
}

public sealed class AnchorRowMap : ClassMap<AnchorRow>
{
    public AnchorRowMap()
    {
        Map(x => x.Chapter).Index(0).Name("Chapter");
        Map(x => x.Words).Index(1).Name("Words");
        Map(x => x.Fingerprint).Index(2).Name("Fingerprint");
    }
}
