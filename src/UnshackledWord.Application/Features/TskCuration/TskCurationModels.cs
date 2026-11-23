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
}

public sealed class TskCurationOutcome
{
    public TskCurationStatus Status { get; private init; }
    public TskCurationResponse? Response { get; private init; }
    public string? Message { get; private init; }

    public static TskCurationOutcome Ok(TskCurationResponse response) => new() { Status = TskCurationStatus.Ok, Response = response };

    public static TskCurationOutcome NotFound(string message) => new() { Status = TskCurationStatus.NotFound, Message = message };

    public static TskCurationOutcome Forbidden(string message) => new() { Status = TskCurationStatus.Forbidden, Message = message };

    public static TskCurationOutcome BadRequest(string message) => new() { Status = TskCurationStatus.BadRequest, Message = message };

    public static TskCurationOutcome Conflict(string message) => new() { Status = TskCurationStatus.Conflict, Message = message };
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
}
