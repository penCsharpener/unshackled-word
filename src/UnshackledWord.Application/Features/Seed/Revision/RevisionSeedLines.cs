using UnshackledWord.Application.Features.Seed.Anchoring;

namespace UnshackledWord.Application.Features.Seed.Revision;

/// <summary>One line of <c>assets/seed/revision/levels.csv</c>: a literalness preset (roadmap 0011.11). The base revision (unrevised Elb1871) is the line with code <c>elb1871</c>.</summary>
public sealed class RevisionLevelSeedLine
{
    public int Level { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameDe { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? DescriptionDe { get; set; }
    public string? DescriptionEn { get; set; }
    public bool IsDefault { get; set; }
}

/// <summary>
/// One line of <c>assets/seed/revision/verses/NN-Book.csv</c>: the active variant of one verse at one level. The text itself is the data
/// (Elb1871 is public domain, the revision is CC0); words, base links and versions are derived on import.
/// </summary>
public sealed class RevisionVerseSeedLine
{
    /// <summary><c>HebRefId</c> (<c>BBCCCVVV</c>) of the Elb1871 verse the variant replaces.</summary>
    public int Ref { get; set; }

    public int Level { get; set; }

    /// <summary>Level of the variant this one was started from (revision of a revision); null = started from Elb1871.</summary>
    public int? BasedOnLevel { get; set; }

    /// <summary>12 hex of the Elb1871 verse fingerprint when the variant was written; kept so a changed base text stays detectable.</summary>
    public string BaseHash { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
    public string? Note { get; set; }

    /// <summary>A writer saved the mapping form of this variant (carried links confirmed). Not in the roadmap's column list: without it a round trip would lose the review state.</summary>
    public bool Reviewed { get; set; }
}

/// <summary>
/// One line of <c>assets/seed/revision/mapping/NN-Book.csv</c>: mapping format 2 of roadmap 0010.8 for the words of a variant plus
/// <c>Level</c> and <c>Origin</c>. The German side is keyed by position and form inside the variant's own text; the original side is
/// a hash instead of the STEP text (never redistributed).
/// </summary>
public sealed class RevisionMappingSeedLine
{
    public int Ref { get; set; }
    public int Level { get; set; }
    public int Pos { get; set; }
    public string Form { get; set; } = string.Empty;
    public string Part { get; set; } = string.Empty;
    public int? SrcRef { get; set; }
    public int? SrcPos { get; set; }
    public string SrcHash { get; set; } = string.Empty;
    public bool Added { get; set; }
    public int? ParentPos { get; set; }
    public string ParentForm { get; set; } = string.Empty;

    /// <summary>carried, manual or seed (see <c>RevisionMapping."Origin"</c>); kept as it was exported.</summary>
    public string Origin { get; set; } = string.Empty;

    public bool HasSource => SrcRef.HasValue && SrcPos.HasValue;
    public bool HasParent => ParentPos.HasValue;
    public SourceKey? Source => HasSource ? new SourceKey(SrcRef!.Value, SrcPos!.Value, SrcHash) : null;
}
