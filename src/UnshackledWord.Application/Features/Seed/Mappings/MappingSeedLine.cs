using UnshackledWord.Application.Features.Seed.Anchoring;

namespace UnshackledWord.Application.Features.Seed.Mappings;

/// <summary>
/// One line of <c>assets/Elb1871Mappings/NN-Book.csv</c> in format 2: one mapping edge between a German word and (optionally) a STEP
/// word, or an added German word pointing at its parent. No database id; the STEP word is a <see cref="SourceKey"/> (hash instead of
/// text, because STEP text is never redistributed).
/// </summary>
public sealed class MappingSeedLine
{
    /// <summary><c>HebRefId</c> of the German word (<c>BBCCCVVV</c> as a number).</summary>
    public int Ref { get; set; }

    /// <summary><c>PositionInVerse</c> of the German word.</summary>
    public int Pos { get; set; }

    /// <summary><c>PlainWord</c> of the German word (public domain Elb1871 text).</summary>
    public string Form { get; set; } = string.Empty;

    /// <summary>Part of a compound word the edge belongs to (<c>Schaf</c> of <c>Schafhirten</c>); empty for the whole word.</summary>
    public string Part { get; set; } = string.Empty;

    /// <summary>STEP <c>LxxRefId</c> of the source word; null when the edge has no source word.</summary>
    public int? SrcRef { get; set; }

    public int? SrcPos { get; set; }

    /// <summary>8 hex digits of <see cref="SourceKey.Hash"/>; empty when the edge has no source word.</summary>
    public string SrcHash { get; set; } = string.Empty;

    /// <summary>The German word was added by the translator (has no original-language counterpart of its own).</summary>
    public bool Added { get; set; }

    public int? ParentPos { get; set; }

    public string ParentForm { get; set; } = string.Empty;

    /// <summary>Verse of the parent word when it is not the verse of the word itself (232 rows on the maintainer's database); null otherwise.</summary>
    public int? ParentRef { get; set; }

    public bool HasSource => SrcRef.HasValue && SrcPos.HasValue;

    public bool HasParent => ParentPos.HasValue;

    public SourceKey? Source => HasSource ? new SourceKey(SrcRef!.Value, SrcPos!.Value, SrcHash) : null;

    public int EffectiveParentRef => ParentRef ?? Ref;
}
