using UnshackledWord.Domain.WebApi.Elberfelder.GetWordDetail;
namespace UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;

public sealed class GetVerseMappingResponse
{
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }

    /// <summary>"greek" (New Testament) or "hebrew" (Old Testament).</summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>True only while the write gate (MappingEditing:Enabled) is open and the caller has a write role (admin/maintainer); otherwise PUT answers 403.</summary>
    public bool EditingEnabled { get; set; }

    /// <summary>Elberfelder words of the verse in reading order.</summary>
    public List<VerseMappingGermanWord> GermanWords { get; set; } = [];

    /// <summary>STEP Greek/Hebrew words of the verse in reading order (read live from the STEP tables).</summary>
    public List<VerseMappingOriginalWord> OriginalWords { get; set; } = [];

    public List<VerseMappingLink> Links { get; set; } = [];
}

public sealed class VerseMappingGermanWord
{
    public int Id { get; set; }
    public int PositionInVerse { get; set; }
    public string Text { get; set; } = string.Empty;
}

public sealed class VerseMappingOriginalWord
{
    public int Id { get; set; }

    /// <summary>StepGreekWords/StepHebrewWords.PositionInVerse.</summary>
    public int Position { get; set; }

    public string Original { get; set; } = string.Empty;
    public string? Transliteration { get; set; }
    public string? Lemma { get; set; }
    public string? Strongs { get; set; }
    public string? Gloss { get; set; }
    public string? MorphologyCode { get; set; }

    /// <summary>Decoded <see cref="MorphologyCode"/>; only filled by the chapter originals read (hover info), null (omitted) otherwise.</summary>
    public MorphologyResponse? Morphology { get; set; }

    /// <summary>
    /// Hebrew only: the morphemes of the word in order (וַיֹּאמֶר = H9001 + H0559), punctuation (H9014–H9018) left out. A link picks one with
    /// <see cref="VerseMappingLink.StrongsNumber"/>. Null (omitted) for Greek and in the chapter originals read.
    /// </summary>
    public List<VerseMappingMorpheme>? Morphemes { get; set; }
}

/// <summary>One morpheme of a Hebrew word (one tag of StepHebrewWords.ExpandedStrongTags).</summary>
public sealed class VerseMappingMorpheme
{
    /// <summary>Strong's tag as STEP writes it, braces stripped (<c>H9001</c>, <c>H0430G</c>).</summary>
    public string Strongs { get; set; } = string.Empty;

    /// <summary>True for the braced main morpheme; a link without <c>strongsNumber</c> renders this one.</summary>
    public bool IsMain { get; set; }

    public string? Original { get; set; }
    public string? Gloss { get; set; }
}

/// <summary>
/// One mapping row. A normal link has <see cref="StepWordId"/> and no parent. An added word
/// (<see cref="IsAddedWord"/>) has a <see cref="ParentElbWordId"/> and no original word. A link of a compound word
/// (<c>Gerstenernte</c> = <c>Gersten</c> + <c>ernte</c>) additionally carries its part text and its order inside the word; all links of one
/// German word then have parts, the orders run 1..n without gaps and the parts in order spell the word. Both are null for a whole-word link.
/// </summary>
public sealed class VerseMappingLink
{
    public int ElbWordId { get; set; }
    public int? StepWordId { get; set; }
    public bool IsAddedWord { get; set; }
    public int? ParentElbWordId { get; set; }

    /// <summary>1-based order of the part inside the German word; set together with <see cref="GermanWordPart"/>, otherwise null.</summary>
    public int? PartOrder { get; set; }

    /// <summary>The part of the German word this link belongs to (<c>ernte</c> of <c>Gerstenernte</c>); null for a whole-word link.</summary>
    public string? GermanWordPart { get; set; }

    /// <summary>
    /// <c>head</c> (carries the meaning of the source morpheme, counted) or <c>unit</c> (belongs to the head's German form: <c>habe</c> in
    /// <c>habe getauft</c>, <c>hinaus</c> in <c>ging hinaus</c>), roadmap 0018. Always set in responses. In a save, null means head; a client that
    /// knows roles sends it on every real link. A save in which no link sets <c>role</c>, <c>strongsNumber</c>, <c>partLemma</c> or
    /// <c>allowMultiHead</c> is a legacy save: the stored values of the same (elbWordId, stepWordId) are kept and the one-head rule is not checked.
    /// </summary>
    public string? Role { get; set; }

    /// <summary>
    /// Hebrew only: the morpheme of the original word this link renders (one of <see cref="VerseMappingOriginalWord.Morphemes"/>,
    /// e.g. <c>H9001</c> for <c>Und</c> of וַיֹּאמֶר); null = the main morpheme. Greek links omit it (their Strong's is the word's own).
    /// </summary>
    public string? StrongsNumber { get; set; }

    /// <summary>Lemma of the compound part (<c>Gerste</c> of <c>Gersten</c>); only together with <see cref="GermanWordPart"/>.</summary>
    public string? PartLemma { get; set; }

    /// <summary>
    /// Accepts more than one head on this link's source morpheme (idioms like <c>zur Antwort geben</c>). One head link with true is enough;
    /// it is stored on every head of that morpheme and comes back true on each of them. Null/false otherwise.
    /// </summary>
    public bool? AllowMultiHead { get; set; }
}
