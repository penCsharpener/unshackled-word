namespace UnshackledWord.Application.Features.LinkRoles.Backfill;

/// <summary>Data access of the link-roles backfill (roadmap 0018.4). Apply and undo each run in one transaction.</summary>
public interface ILinkRoleBackfillRepository
{
    /// <summary>Books that have Elb1871 words, in canonical order.</summary>
    Task<IReadOnlyList<int>> GetBookIdsAsync(CancellationToken ct);

    Task<LinkRoleBookData> LoadBookAsync(int bookId, CancellationToken ct);

    /// <summary>Every distinct Elb1871Morphology lemma, lower case (rule 3b's "combination exists" set).</summary>
    Task<IReadOnlySet<string>> LoadKnownLemmasAsync(CancellationToken ct);

    /// <summary>Agent runs that are queued, running or waiting for a provider batch.</summary>
    Task<int> CountActiveAgentRunsAsync(CancellationToken ct);

    /// <summary>The latest applied backfill change set that was not undone, or null.</summary>
    Task<LinkRoleChangeSetInfo?> GetAppliedAsync(CancellationToken ct);

    /// <summary><c>DqRefreshState.LinkRolesAppliedAt</c>, null when not set or the column does not exist yet.</summary>
    Task<DateTime?> GetLinkRolesAppliedAtAsync(CancellationToken ct);

    /// <summary>
    /// Writes the change set: one Elb1871MorphologyChangeSets row (kind <see cref="LinkRoleChangeSetKinds.Apply"/>), the mapping rows with
    /// before/after, the lemma edits; sets <c>LinkRolesAppliedAt</c>. Throws (and rolls back) when a row no longer has its before values.
    /// </summary>
    Task<LinkRoleApplyResult> ApplyAsync(IReadOnlyList<MappingRowChange> mappings, IReadOnlyList<LemmaRowChange> lemmas, int userId, string note, CancellationToken ct);

    /// <summary>Restores the before values of the change set where the row still has its after values; resets <c>LinkRolesAppliedAt</c>.</summary>
    Task<LinkRoleUndoResult> UndoAsync(int changeSetId, int userId, CancellationToken ct);
}
