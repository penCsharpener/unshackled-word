namespace UnshackledWord.Application.Features.TskCuration;

/// <summary>A <c>TskCurationProposals</c> row (roadmap 0024.6): the full desired state of one link plus the decision.</summary>
public sealed record TskProposalRecord(
    int Id,
    TskLinkKey Key,
    TskCurationState State,
    bool Delete,
    DateTimeOffset? BaseUpdatedAt,
    int UserId,
    DateTimeOffset CreatedAt,
    string ProposalState,
    int? DecidedByUserId,
    DateTimeOffset? DecidedAt,
    string? Reason);

/// <summary>A pending proposal with what the review list needs: author name, whether TSK has the link, the current curation row (null = none).</summary>
public sealed record TskProposalReviewRecord(TskProposalRecord Proposal, string AuthorDisplayName, bool InTsk, TskCurationRecord? Current)
{
    /// <summary>The link's curation changed since the proposal was made.</summary>
    public bool Stale => Current?.UpdatedAt != Proposal.BaseUpdatedAt;
}

/// <summary>
/// One link of a proposal write: the desired state and the base it was computed from. <paramref name="Withdraw"/> = the desired state equals the current
/// one: only the user's pending proposal for the key is superseded, nothing new is stored.
/// </summary>
public sealed record TskProposalWrite(TskLinkKey Key, TskCurationState State, bool Delete, DateTimeOffset? BaseUpdatedAt, bool Withdraw);

public enum TskProposalDecisionStatus
{
    Ok,
    NotFound,
    NotPending,

    /// <summary>Accept only: the link changed since the proposal was made and <c>force</c> was not set; nothing changed.</summary>
    Stale,
}

/// <summary>Result of accepting or rejecting: the proposal after the decision and (accept) the link's curation row after applying (null = no row).</summary>
public sealed record TskProposalDecision(TskProposalDecisionStatus Status, TskProposalRecord? Proposal = null, TskCurationRecord? Curation = null);

/// <summary>Table <c>TskCurationProposals</c> (migration 0046).</summary>
public interface ITskCurationProposalRepository
{
    Task<TskProposalRecord?> GetAsync(int id, CancellationToken token = default);

    /// <summary>The user's pending proposals for source verses <paramref name="fromHebRefId"/>..<paramref name="toHebRefId"/> (a chapter or one verse).</summary>
    Task<IReadOnlyList<TskProposalRecord>> GetPendingOfUserAsync(int userId, int fromHebRefId, int toHebRefId, CancellationToken token = default);

    /// <summary>
    /// In ONE transaction (advisory lock per source verse, as the curation writes): per write supersedes the user's pending proposal of that key and,
    /// unless <see cref="TskProposalWrite.Withdraw"/>, inserts the new pending one. Returns the new proposal per write (null for a withdrawal), in order.
    /// </summary>
    Task<IReadOnlyList<TskProposalRecord?>> ProposeAsync(int userId, IReadOnlyList<TskProposalWrite> writes, CancellationToken token = default);

    /// <summary>Every pending proposal, ordered by source verse, target, creation time.</summary>
    Task<IReadOnlyList<TskProposalReviewRecord>> GetPendingAsync(int limit, CancellationToken token = default);

    /// <summary>The user's proposals, newest first, all states.</summary>
    Task<IReadOnlyList<TskProposalRecord>> GetOfUserAsync(int userId, int limit, CancellationToken token = default);

    /// <summary>
    /// In ONE transaction: locks the proposal, checks it is pending and (unless <paramref name="force"/>) not stale, applies the desired state through the
    /// curation write path (audit <c>Source</c> <c>proposal</c>, <c>UserId</c> = <paramref name="maintainerId"/>, <c>proposalId</c> in <c>After</c>) and
    /// marks it accepted. Other pending proposals of the link stay pending (they turn stale when the state changed).
    /// </summary>
    Task<TskProposalDecision> AcceptAsync(int id, int maintainerId, bool force, CancellationToken token = default);

    /// <summary>Marks a pending proposal rejected with the optional reason; the curation is untouched.</summary>
    Task<TskProposalDecision> RejectAsync(int id, int maintainerId, string? reason, CancellationToken token = default);
}
