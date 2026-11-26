namespace UnshackledWord.Application.Features.Lxx;

/// <summary>One distinct NT analysis of a form: <c>StepGreekWords</c> grouped by (Greek, Lemma, DisambiguatedStrongs, Morphology).</summary>
public sealed record NtFormAnalysis(string Greek, string? Lemma, string? Strongs, string? Morphology);

/// <summary>An LXX word as pass 1 needs it.</summary>
public sealed record LxxPrefillWord(int Id, int BibleBookId, string TextForm, string Greek);

/// <summary>An existing tag row (any status or source), so pass 1 knows what it must not touch.</summary>
public sealed record LxxExistingTag(int LxxWordId, string Status, string Source);

/// <summary>A pass-1 proposal to write.</summary>
public sealed record LxxNtFormProposal(int LxxWordId, string Lemma, string Strongs, string Morphology);

/// <summary>What <see cref="ILxxNtFormRepository.ApplyAsync"/> changed.</summary>
public sealed record LxxNtFormApplyResult(int Inserted, int Updated, int Deleted)
{
    public int Changed => Inserted + Updated + Deleted;
}

/// <summary>Tag rows per (Status, Source).</summary>
public sealed record LxxTagCount(string Status, string Source, int Count);

/// <summary>Counts for the maintainer's LXX card (0028.6f).</summary>
public sealed record LxxStatusCounts(int Verses, int Words, IReadOnlyList<LxxTagCount> Tags);

/// <summary>Reads and writes of pass 1 (roadmap 0028.6): bulk, one transaction for the write.</summary>
public interface ILxxNtFormRepository
{
    Task<IReadOnlyList<NtFormAnalysis>> GetNtAnalysesAsync(CancellationToken token = default);

    Task<IReadOnlyList<LxxPrefillWord>> GetLxxWordsAsync(CancellationToken token = default);

    Task<IReadOnlyList<LxxExistingTag>> GetTagsAsync(CancellationToken token = default);

    Task<LxxStatusCounts> GetStatusCountsAsync(CancellationToken token = default);

    /// <summary>
    /// Makes the <c>nt-form</c>/<c>proposed</c> rows equal to <paramref name="proposals"/>: inserts new ones, updates changed ones, deletes those no
    /// longer proposed. Rows with another source or status (<c>confirmed</c>, <c>ai</c>, <c>human</c>) are never touched. One transaction.
    /// </summary>
    Task<LxxNtFormApplyResult> ApplyAsync(IReadOnlyList<LxxNtFormProposal> proposals, CancellationToken token = default);
}
