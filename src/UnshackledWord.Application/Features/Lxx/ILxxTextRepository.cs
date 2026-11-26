namespace UnshackledWord.Application.Features.Lxx;

/// <summary>Write access to the Septuagint text tables <c>LxxVerses</c>/<c>LxxWords</c> (migration 0050, roadmap 0028.3).</summary>
public interface ILxxTextRepository
{
    /// <summary>True when <c>LxxVerses</c> has no row (the import gate).</summary>
    Task<bool> IsEmptyAsync(CancellationToken token = default);

    /// <summary>
    /// Inserts all verses and their words in one transaction, only while <c>LxxVerses</c> is empty (checked again inside the transaction).
    /// Returns the number of verses inserted, 0 when the table already had rows.
    /// </summary>
    Task<int> InsertAsync(IReadOnlyList<LxxVerseText> verses, CancellationToken token = default);
}
