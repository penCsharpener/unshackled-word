using UnshackledWord.Application.Features.Notes;
using UnshackledWord.Domain.WebApi.Tsk;

namespace UnshackledWord.Application.Features.TskCuration;

/// <summary>Key of one TSK link in Elberfelder numbering, normalised like the stored <c>Tsk</c> keys (end null = single verse). Never <c>Tsk.Id</c>.</summary>
public sealed record TskLinkKey(int SourceHebRefId, int TargetStartHebRefId, int? TargetEndHebRefId);

/// <summary>The curation columns of a <c>TskCurations</c> row; also the shape of <c>TskCurationAudit.Before/After</c>.</summary>
public sealed record TskCurationState(string Origin, string? Rating, string? Comment, int? StartPosition, int? EndPosition, string? QuotedText)
{
    /// <summary>A TSK link nobody curated: it has no row.</summary>
    public static readonly TskCurationState Plain = new(TskOrigins.Tsk, null, null, null, null, null);

    /// <summary>Neutral, uncommented, unlinked TSK link: the row is deleted so the table stays a true diff.</summary>
    public bool IsDefault => Origin == TskOrigins.Tsk && Rating is null && Comment is null && StartPosition is null && EndPosition is null && QuotedText is null;
}

/// <summary>A link of a source verse: a <c>Tsk</c> row (<paramref name="InTsk"/>) or only an added link.</summary>
public sealed record TskSourceLink(TskLinkKey Key, bool InTsk);

/// <summary>A <c>TskCurations</c> row.</summary>
public sealed record TskCurationRecord(TskLinkKey Key, TskCurationState State, DateTimeOffset UpdatedAt, int? UpdatedByUserId);

/// <summary>Values of <c>TskCurationAudit.Source</c>.</summary>
public static class TskCurationSources
{
    public const string Edit = "edit";
    public const string Proposal = "proposal";
    public const string Import = "import";
}

public enum TskCurationStatus
{
    Ok,
    NotFound,
    Forbidden,

    /// <summary>Malformed target or a request that does not apply to this key (roadmap 0024.4: deleting a TSK link).</summary>
    BadRequest,

    /// <summary>The added link already exists, in TSK or as an earlier added link (roadmap 0024.4).</summary>
    Conflict,

    /// <summary>A non-maintainer's request was stored as a pending proposal (roadmap 0024.6): HTTP 202, nothing curated yet.</summary>
    Proposed,
}

public sealed class TskCurationOutcome
{
    public TskCurationStatus Status { get; private init; }
    public TskCurationResponse? Response { get; private init; }

    /// <summary>Set with <see cref="TskCurationStatus.Proposed"/>.</summary>
    public TskProposalResponse? Proposal { get; private init; }

    public string? Message { get; private init; }

    public static TskCurationOutcome Ok(TskCurationResponse response) => new() { Status = TskCurationStatus.Ok, Response = response };

    public static TskCurationOutcome Proposed(TskProposalResponse proposal) => new() { Status = TskCurationStatus.Proposed, Proposal = proposal };

    public static TskCurationOutcome NotFound(string message) => new() { Status = TskCurationStatus.NotFound, Message = message };

    public static TskCurationOutcome Forbidden(string message) => new() { Status = TskCurationStatus.Forbidden, Message = message };

    public static TskCurationOutcome BadRequest(string message) => new() { Status = TskCurationStatus.BadRequest, Message = message };

    public static TskCurationOutcome Conflict(string message) => new() { Status = TskCurationStatus.Conflict, Message = message };
}

/// <summary>Result of the word-link endpoints (roadmap 0024.5): the states of all requested links, or why nothing was written.</summary>
public sealed class TskWordLinkOutcome
{
    public TskCurationStatus Status { get; private init; }
    public TskWordLinkResponse? Response { get; private init; }

    /// <summary>Set with <see cref="TskCurationStatus.Proposed"/>: one proposal per key that changes (keys already in the requested state are omitted).</summary>
    public TskProposalsCreatedResponse? Proposals { get; private init; }

    public string? Message { get; private init; }

    public static TskWordLinkOutcome Proposed(List<TskProposalResponse> proposals) =>
        new() { Status = TskCurationStatus.Proposed, Proposals = new TskProposalsCreatedResponse { Proposals = proposals } };

    public static TskWordLinkOutcome Ok(List<TskCurationResponse> links) => new() { Status = TskCurationStatus.Ok, Response = new TskWordLinkResponse { Links = links } };

    public static TskWordLinkOutcome Refused(TskCurationStatus status, string message) => new() { Status = status, Message = message };
}

public interface ITskCurationRepository
{
    /// <summary>True when <c>Tsk</c> has a row with exactly this stored key.</summary>
    Task<bool> TskLinkExistsAsync(TskLinkKey key, CancellationToken token = default);

    Task<TskCurationRecord?> GetAsync(TskLinkKey key, CancellationToken token = default);

    /// <summary>True when <c>Elb1871Verses</c> has this verse (Elberfelder ref id).</summary>
    Task<bool> VerseExistsAsync(int hebRefId, CancellationToken token = default);

    /// <summary>
    /// In one transaction: reads the current state (null = no row), applies <paramref name="change"/>, writes the row (deletes it when the result is
    /// <see cref="TskCurationState.IsDefault"/>) and appends one audit row with before/after. Nothing is written when the state does not change.
    /// Returns the row after the change, null when there is none.
    /// </summary>
    Task<TskCurationRecord?> ApplyAsync(TskLinkKey key, Func<TskCurationState, TskCurationState> change, int? userId, string source, CancellationToken token = default);

    /// <summary>Every link of one source verse (roadmap 0024.5): the stored keys of its <c>Tsk</c> rows and its added links (<c>TskCurations</c> origin <c>added</c>, <c>InTsk</c> false).</summary>
    Task<IReadOnlyList<TskSourceLink>> GetLinksOfSourceAsync(int sourceHebRefId, CancellationToken token = default);

    /// <summary>The Elb1871 words of one verse, ordered by position (roadmap 0024.5: the quote of a word link).</summary>
    Task<IReadOnlyList<NoteWordRow>> GetElbWordsAsync(int hebRefId, CancellationToken token = default);

    /// <summary>
    /// <see cref="ApplyAsync(TskLinkKey, Func{TskCurationState, TskCurationState}, int?, string, CancellationToken)"/> for several links in ONE transaction
    /// (one audit row per changed link). Returns the row after the change per key, in the order of <paramref name="keys"/> (null = no row).
    /// </summary>
    Task<IReadOnlyList<TskCurationRecord?>> ApplyManyAsync(IReadOnlyList<TskLinkKey> keys, Func<TskLinkKey, TskCurationState, TskCurationState> change, int? userId,
        string source, CancellationToken token = default);
}
