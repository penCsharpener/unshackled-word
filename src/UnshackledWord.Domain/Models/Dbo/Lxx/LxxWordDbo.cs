namespace UnshackledWord.Domain.Models.Dbo.Lxx;

/// <summary>One Greek word of a Septuagint verse (table <c>LxxWords</c>, migration 0050, roadmap 0028).</summary>
public sealed class LxxWordDbo
{
    public const string DboName = "\"unshackled-word\".\"LxxWords\"";

    public int Id { get; set; }
    public int LxxVerseId { get; set; }

    /// <summary>1..n within the verse after filtering (marker tokens, editorial marks and acrostic letter names removed).</summary>
    public int Position { get; set; }

    /// <summary>As in Swete, with punctuation.</summary>
    public string Greek { get; set; } = default!;

    public string GreekNoDiacritics { get; set; } = default!;

    /// <summary><c>SourceKey.Hash</c> of <see cref="Greek"/>, the seed anchor.</summary>
    public string SrcHash { get; set; } = default!;
}
