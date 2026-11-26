using UnshackledWord.Domain.WebApi.Elberfelder.GetWordDetail;

namespace UnshackledWord.Domain.WebApi.Lxx;

/// <summary><c>GET api/lxx/word/{lxxWordId}</c> (roadmap 0028.5): the word box of one Septuagint word.</summary>
public sealed class GetLxxWordRequest
{
    public int LxxWordId { get; set; }
}

/// <summary>
/// One LXX word: the Swete form and its Rahlfs reference, and when tagged (confirmed, or proposed for Admin/Maintainer) lemma, Strong's, parse and
/// the TBESG lexicon entry. An untagged word carries only form and reference.
/// </summary>
public sealed class GetLxxWordResponse
{
    public int Id { get; set; }
    public int LxxVerseId { get; set; }
    public int Position { get; set; }

    /// <summary>Swete's form with punctuation, e.g. ἀρχῇ.</summary>
    public string Greek { get; set; } = "";

    /// <summary>Rahlfs reference as <c>Gen.1:1</c> or <c>1Ki.2:35.1</c> (TVTMS book code, as in <c>api/lxx/chapter</c>); the client formats it.</summary>
    public string Ref { get; set; } = "";

    /// <summary>Rahlfs counting.</summary>
    public int BibleBookId { get; set; }

    public int Chapter { get; set; }
    public int Verse { get; set; }
    public int Subverse { get; set; }

    /// <summary>"" | "og" | "th".</summary>
    public string TextForm { get; set; } = "";

    public bool IsAddition { get; set; }

    /// <summary>The Elberfelder verse the word is read with (<c>HebRefId</c>, for additions <c>AfterHebRefId</c>); omitted for absent units.</summary>
    public int? HebRefId { get; set; }

    /// <summary>Omitted when the word is untagged (or only proposed and the caller is not Admin/Maintainer).</summary>
    public LxxWordTagDetailResponse? Tag { get; set; }
}

public sealed class LxxWordTagDetailResponse
{
    public string Lemma { get; set; } = "";
    public string Strongs { get; set; } = "";

    /// <summary>Short English gloss from TBESG; omitted when the lexicon has no entry.</summary>
    public string? Gloss { get; set; }

    /// <summary>The code and its decoded keys (same shape as the NT word detail).</summary>
    public MorphologyResponse Morphology { get; set; } = new();

    /// <summary>"Nomen Dativ Singular feminin"; omitted when the stored code does not parse (GreekMorphologyCode).</summary>
    public string? ParseGerman { get; set; }

    /// <summary>"noun dative singular feminine"; omitted when the stored code does not parse.</summary>
    public string? ParseEnglish { get; set; }

    /// <summary>confirmed | proposed (proposed only reaches Admin/Maintainer).</summary>
    public string Status { get; set; } = "";

    /// <summary>Only for Admin/Maintainer: nt-form | ai | human.</summary>
    public string? Source { get; set; }

    /// <summary>The TBESG entry of <see cref="Strongs"/> (lemma, gloss, definition with linked references); omitted when there is none.</summary>
    public LexiconSummaryResponse? Lexicon { get; set; }
}
