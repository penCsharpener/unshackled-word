namespace UnshackledWord.Domain.Models.Dbo.Lxx;

/// <summary>
/// One Septuagint verse: a mapped Swete unit in Rahlfs numbering (table <c>LxxVerses</c>, migration 0050, roadmap 0028). Reading order is
/// <see cref="SortOrder"/> (Swete's order), not <see cref="RahlfsRefId"/>.
/// </summary>
public sealed class LxxVerseDbo
{
    public const string DboName = "\"unshackled-word\".\"LxxVerses\"";

    public int Id { get; set; }

    /// <summary>1..39 (Daniel = 27 for both text forms).</summary>
    public int BibleBookId { get; set; }

    /// <summary>Empty, <c>og</c> (Daniel Old Greek) or <c>th</c> (Daniel Theodotion).</summary>
    public string TextForm { get; set; } = "";

    /// <summary>Rahlfs chapter; Swete's for an absent row.</summary>
    public int Chapter { get; set; }

    /// <summary>Rahlfs verse (a Psalm title is verse 1); Swete's for an absent row.</summary>
    public int Verse { get; set; }

    public int Subverse { get; set; }

    /// <summary><c>book·10⁸ + chapter·10⁵ + verse·10² + subverse</c>; null only for <c>absent</c>.</summary>
    public long? RahlfsRefId { get; set; }

    /// <summary>Swete's order, unique.</summary>
    public int SortOrder { get; set; }

    public string SweteRef { get; set; } = default!;

    /// <summary>STEP key, English counting; null for additions and absent rows.</summary>
    public int? LxxRefId { get; set; }

    /// <summary>Elberfelder verse; null for additions and absent rows.</summary>
    public int? HebRefId { get; set; }

    /// <summary>Additions: shown after this Elberfelder verse.</summary>
    public int? AfterHebRefId { get; set; }

    /// <summary><c>one</c>, <c>merged</c>, <c>split</c>, <c>addition</c> or <c>absent</c>.</summary>
    public string MapKind { get; set; } = default!;
}
