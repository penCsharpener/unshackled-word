using UnshackledWord.Domain.WebApi.Tsk;

namespace UnshackledWord.Application.Features.TskCuration;

/// <summary>Maintainer edits of TSK links (roadmap 0024.3): rating and comment of one TSK or added link, adding and removing added links (0024.4), each written with an audit row.</summary>
public sealed class TskCurationService
{
    private const string ForbiddenMessage = "Only maintainers can curate cross references.";

    private readonly ITskCurationRepository _repository;

    public TskCurationService(ITskCurationRepository repository)
    {
        _repository = repository;
    }

    /// <param name="canWrite">Admin or Maintainer. Until suggestions exist (0024.6) everyone else is refused.</param>
    /// <remarks>Works on TSK links and on added links (roadmap 0024.4); an added link keeps its row (origin <c>added</c>) when it becomes neutral.</remarks>
    public async Task<TskCurationOutcome> SaveAsync(int userId, bool canWrite, SaveTskCurationRequest request, CancellationToken token = default)
    {
        if (!canWrite)
        {
            return TskCurationOutcome.Forbidden(ForbiddenMessage);
        }

        var key = new TskLinkKey(request.SourceHebRefId, request.TargetStartHebRefId, request.TargetEndHebRefId);
        var added = false;
        if (!await _repository.TskLinkExistsAsync(key, token))
        {
            added = (await _repository.GetAsync(key, token))?.State.Origin == TskOrigins.Added;
            if (!added)
            {
                return TskCurationOutcome.NotFound("No TSK cross reference or added link with this source and target.");
            }
        }

        var rating = string.IsNullOrWhiteSpace(request.Rating) ? null : request.Rating.Trim();
        var comment = NormaliseComment(request.Comment);
        // An added link deleted meanwhile must not come back as a curated TSK link that TSK does not have: then nothing changes.
        var record = await _repository.ApplyAsync(key,
            state => added && state.Origin != TskOrigins.Added ? state : state with { Rating = rating, Comment = comment },
            userId, TskCurationSources.Edit, token);
        if (added && record is null)
        {
            return TskCurationOutcome.NotFound("The added link was removed meanwhile.");
        }

        return TskCurationOutcome.Ok(ToResponse(key, record));
    }

    /// <summary>
    /// Adds a link TSK lacks (roadmap 0024.4): <c>TskCurations</c> row with origin <c>added</c> plus an audit row. The request is validated
    /// (<see cref="AddTskCurationRequestValidator"/>); the target is parsed again here to build the key.
    /// </summary>
    public async Task<TskCurationOutcome> AddAsync(int userId, bool canWrite, AddTskCurationRequest request, CancellationToken token = default)
    {
        if (!canWrite)
        {
            return TskCurationOutcome.Forbidden(ForbiddenMessage);
        }

        var target = TskAddedTarget.Parse(request.Reference);
        if (target.Error is not null)
        {
            return TskCurationOutcome.BadRequest(target.Error);
        }

        var key = new TskLinkKey(request.SourceHebRefId, target.Start!.Value, target.End);
        if (key.TargetStartHebRefId == key.SourceHebRefId && key.TargetEndHebRefId is null)
        {
            return TskCurationOutcome.BadRequest("A verse cannot refer to itself.");
        }

        if (!await _repository.VerseExistsAsync(request.SourceHebRefId, token))
        {
            return TskCurationOutcome.NotFound("The source verse does not exist in the Elberfelder 1871.");
        }

        if (await _repository.TskLinkExistsAsync(key, token))
        {
            return TskCurationOutcome.Conflict("TSK already has this cross reference.");
        }

        var state = new TskCurationState(TskOrigins.Added, null, NormaliseComment(request.Comment), null, null, null);
        var inserted = false;
        var record = await _repository.ApplyAsync(key, current =>
        {
            // No row reads as Plain; any existing row (an earlier added link) stays untouched.
            if (current != TskCurationState.Plain)
            {
                return current;
            }

            inserted = true;
            return state;
        }, userId, TskCurationSources.Edit, token);

        return inserted
            ? TskCurationOutcome.Ok(ToResponse(key, record))
            : TskCurationOutcome.Conflict("This cross reference was already added.");
    }

    /// <summary>Removes an added link (roadmap 0024.4) with an audit row. A TSK link cannot be deleted (hide it instead).</summary>
    public async Task<TskCurationOutcome> DeleteAddedAsync(int userId, bool canWrite, DeleteTskCurationRequest request, CancellationToken token = default)
    {
        if (!canWrite)
        {
            return TskCurationOutcome.Forbidden(ForbiddenMessage);
        }

        var key = new TskLinkKey(request.SourceHebRefId, request.TargetStartHebRefId, request.TargetEndHebRefId);
        var current = await _repository.GetAsync(key, token);
        if (current?.State.Origin != TskOrigins.Added)
        {
            return current is null && !await _repository.TskLinkExistsAsync(key, token)
                ? TskCurationOutcome.NotFound("No added link with this source and target.")
                : TskCurationOutcome.BadRequest("This is a TSK cross reference; it cannot be deleted, only hidden.");
        }

        var deleted = false;
        await _repository.ApplyAsync(key, state =>
        {
            if (state.Origin != TskOrigins.Added)
            {
                return state;
            }

            deleted = true;
            return TskCurationState.Plain;
        }, userId, TskCurationSources.Edit, token);

        return deleted
            ? TskCurationOutcome.Ok(ToResponse(key, null))
            : TskCurationOutcome.NotFound("No added link with this source and target.");
    }

    /// <summary>Trimmed; empty or whitespace only is no comment.</summary>
    public static string? NormaliseComment(string? comment) => string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

    public static TskCurationResponse ToResponse(TskLinkKey key, TskCurationRecord? record) => new()
    {
        SourceHebRefId = key.SourceHebRefId,
        TargetStartHebRefId = key.TargetStartHebRefId,
        TargetEndHebRefId = key.TargetEndHebRefId,
        Origin = record?.State.Origin ?? TskOrigins.Tsk,
        Rating = record?.State.Rating,
        Comment = record?.State.Comment,
        UpdatedAt = record?.UpdatedAt,
    };
}
