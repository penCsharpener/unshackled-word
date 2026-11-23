namespace UnshackledWord.Domain.WebApi.Tsk;

/// <summary>Values of <c>proposal.state</c> (roadmap 0024.6).</summary>
public static class TskProposalStates
{
    public const string Pending = "pending";
    public const string Accepted = "accepted";
    public const string Rejected = "rejected";

    /// <summary>Replaced by a newer proposal of the same user for the same link, or withdrawn (the user asked for the current state again).</summary>
    public const string Superseded = "superseded";
}

/// <summary>
/// The full desired state of one link in a proposal (roadmap 0024.6): what the link looks like after accepting. Null properties are omitted:
/// no <c>rating</c> = neutral, no <c>comment</c> = none, no <c>word</c> = not attached to a word. <c>delete</c> true (else omitted) = withdraw the added link.
/// </summary>
public sealed class TskProposedStateResponse
{
    /// <summary><c>tsk</c> or <c>added</c> (a proposed new link, or a change of an added link).</summary>
    public string Origin { get; set; } = TskOrigins.Tsk;
    public string? Rating { get; set; }
    public string? Comment { get; set; }

    /// <summary>Word link as proposed (positions and quote, <c>state</c> always <c>ok</c>, never <c>mappedWords</c>).</summary>
    public TskWordResponse? Word { get; set; }

    /// <summary>true or omitted.</summary>
    public bool? Delete { get; set; }
}

/// <summary>One proposal as its author sees it: write answers (202) and <c>GET api/tsk/proposals/mine</c>.</summary>
public sealed class TskProposalResponse
{
    public int Id { get; set; }
    public int SourceHebRefId { get; set; }
    public int TargetStartHebRefId { get; set; }

    /// <summary>Omitted for a single verse.</summary>
    public int? TargetEndHebRefId { get; set; }

    /// <summary><see cref="TskProposalStates"/>.</summary>
    public string State { get; set; } = TskProposalStates.Pending;

    public TskProposedStateResponse Proposed { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When accepted, rejected or superseded; omitted while pending.</summary>
    public DateTimeOffset? DecidedAt { get; set; }

    /// <summary>The maintainer's reason of a rejection; omitted when none was given.</summary>
    public string? Reason { get; set; }
}

/// <summary>202 answer of <c>PUT api/tsk/curation</c>, <c>POST api/tsk/curation/added</c> and <c>DELETE api/tsk/curation/added</c> for non-maintainers.</summary>
public sealed class TskProposalCreatedResponse
{
    public TskProposalResponse Proposal { get; set; } = new();
}

/// <summary>202 answer of <c>PUT/DELETE api/tsk/curation/word</c> for non-maintainers: one proposal per key that changes, in request order.</summary>
public sealed class TskProposalsCreatedResponse
{
    public List<TskProposalResponse> Proposals { get; set; } = [];
}

/// <summary><c>GET api/tsk/proposals/mine</c>: the caller's proposals, newest first (at most 200), all states.</summary>
public sealed class MyTskProposalsResponse
{
    public List<TskProposalResponse> Proposals { get; set; } = [];
}

/// <summary>
/// The caller's own pending proposal on a ref of the chapter cross references (roadmap 0024.6); only the author ever sees it. Same full-state
/// semantics as <see cref="TskProposedStateResponse"/>: omitted <c>rating</c>/<c>comment</c>/<c>word</c> = none proposed.
/// </summary>
public sealed class TskPendingProposalResponse
{
    public int Id { get; set; }
    public string? Rating { get; set; }
    public string? Comment { get; set; }
    public TskWordResponse? Word { get; set; }

    /// <summary>true or omitted: the author proposes to delete this added link.</summary>
    public bool? Delete { get; set; }
}

/// <summary>The current curation state of a link in the review list; null on the item = the link has no curation row (plain TSK, or no link at all).</summary>
public sealed class TskCurrentStateResponse
{
    public string Origin { get; set; } = TskOrigins.Tsk;
    public string? Rating { get; set; }
    public string? Comment { get; set; }
    public TskWordResponse? Word { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>One pending proposal on <c>GET api/maintainer/tsk/proposals</c>.</summary>
public sealed class TskProposalReviewItemResponse
{
    public int Id { get; set; }
    public int SourceHebRefId { get; set; }
    public int TargetStartHebRefId { get; set; }
    public int? TargetEndHebRefId { get; set; }

    /// <summary>true when <c>Tsk</c> has this link (false for added links and proposed new ones).</summary>
    public bool InTsk { get; set; }

    /// <summary>Omitted when the link has no curation row.</summary>
    public TskCurrentStateResponse? Current { get; set; }

    public TskProposedStateResponse Proposed { get; set; } = new();
    public int AuthorUserId { get; set; }
    public string AuthorDisplayName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The link's curation changed since the proposal was made (current <c>updatedAt</c> differs from the base): accepting needs <c>force</c>.</summary>
    public bool Stale { get; set; }
}

/// <summary><c>GET api/maintainer/tsk/proposals</c>: every pending proposal ordered by source verse, target, creation time (at most 1000).</summary>
public sealed class TskProposalReviewListResponse
{
    public List<TskProposalReviewItemResponse> Proposals { get; set; } = [];
}

/// <summary><c>POST api/maintainer/tsk/proposals/{id}/accept</c>; body optional.</summary>
public sealed class AcceptTskProposalRequest
{
    public int Id { get; set; }

    /// <summary>Apply a stale proposal anyway (the maintainer saw the current state).</summary>
    public bool Force { get; set; }
}

/// <summary><c>POST api/maintainer/tsk/proposals/{id}/reject</c>; body optional.</summary>
public sealed class RejectTskProposalRequest
{
    public int Id { get; set; }

    /// <summary>Shown to the author; max 500 characters after trimming, empty = none.</summary>
    public string? Reason { get; set; }
}

/// <summary>Answer of accept: the proposal (now <c>accepted</c>) and the link's resulting curation state.</summary>
public sealed class AcceptTskProposalResponse
{
    public TskProposalResponse Proposal { get; set; } = new();
    public TskCurationResponse Curation { get; set; } = new();
}
