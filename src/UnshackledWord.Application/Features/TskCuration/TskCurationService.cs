using UnshackledWord.Application.Features.Notes;
using UnshackledWord.Domain.WebApi.Tsk;

namespace UnshackledWord.Application.Features.TskCuration;

/// <summary>
/// Edits of TSK links (roadmap 0024.3): rating and comment of one TSK or added link, adding and removing added links (0024.4), word links (0024.5), each
/// written with an audit row. From 0024.6 on every logged-in user may call these: maintainers (<c>canWrite</c>) write directly, everyone else gets a
/// pending <c>TskCurationProposals</c> row holding the full desired state of the link (current state, or their own still-fresh pending proposal, plus the
/// requested change) instead. A request whose desired state equals the current state withdraws the caller's pending proposal of that key.
/// </summary>
public sealed class TskCurationService
{
    private const string ForbiddenMessage = "Only maintainers can curate cross references.";

    private static readonly IReadOnlyDictionary<TskLinkKey, TskProposalRecord> NoPending = new Dictionary<TskLinkKey, TskProposalRecord>();

    private readonly ITskCurationRepository _repository;
    private readonly ITskCurationProposalRepository? _proposals;

    /// <param name="proposals">Null: no suggestions, non-maintainers are refused with 403 (the 0024.3–0024.5 behaviour, kept for older tests).</param>
    public TskCurationService(ITskCurationRepository repository, ITskCurationProposalRepository? proposals = null)
    {
        _repository = repository;
        _proposals = proposals;
    }

    /// <param name="canWrite">Admin or Maintainer. Until suggestions exist (0024.6) everyone else is refused.</param>
    /// <remarks>Works on TSK links and on added links (roadmap 0024.4); an added link keeps its row (origin <c>added</c>) when it becomes neutral.</remarks>
    public async Task<TskCurationOutcome> SaveAsync(int userId, bool canWrite, SaveTskCurationRequest request, CancellationToken token = default)
    {
        if (!canWrite)
        {
            return _proposals is null ? TskCurationOutcome.Forbidden(ForbiddenMessage) : await ProposeSaveAsync(userId, request, token);
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
        if (!canWrite && _proposals is null)
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
        if (!canWrite)
        {
            if (await _repository.GetAsync(key, token) is not null)
            {
                return TskCurationOutcome.Conflict("This cross reference was already added.");
            }

            var proposal = (await _proposals!.ProposeAsync(userId, [new TskProposalWrite(key, state, false, null, false)], token))[0]!;
            return TskCurationOutcome.Proposed(TskProposalMapper.ToResponse(proposal));
        }

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
    /// <remarks>
    /// Non-maintainers: proposes the deletion (<c>delete</c> true, state columns = current state); deleting a link that exists only as the caller's own
    /// pending add proposal withdraws that proposal (answered like a deletion).
    /// </remarks>
    public async Task<TskCurationOutcome> DeleteAddedAsync(int userId, bool canWrite, DeleteTskCurationRequest request, CancellationToken token = default)
    {
        if (!canWrite && _proposals is null)
        {
            return TskCurationOutcome.Forbidden(ForbiddenMessage);
        }

        var key = new TskLinkKey(request.SourceHebRefId, request.TargetStartHebRefId, request.TargetEndHebRefId);
        var current = await _repository.GetAsync(key, token);
        if (!canWrite && current is null)
        {
            var pending = (await PendingOfSourceAsync(userId, key.SourceHebRefId, token)).GetValueOrDefault(key);
            if (pending is { Delete: false, State.Origin: TskOrigins.Added })
            {
                await _proposals!.ProposeAsync(userId, [new TskProposalWrite(key, pending.State, false, null, true)], token);
                return TskCurationOutcome.Ok(ToResponse(key, null));
            }
        }

        if (current?.State.Origin != TskOrigins.Added)
        {
            return current is null && !await _repository.TskLinkExistsAsync(key, token)
                ? TskCurationOutcome.NotFound("No added link with this source and target.")
                : TskCurationOutcome.BadRequest("This is a TSK cross reference; it cannot be deleted, only hidden.");
        }

        if (!canWrite)
        {
            var proposal = (await _proposals!.ProposeAsync(userId, [new TskProposalWrite(key, current.State, true, current.UpdatedAt, false)], token))[0]!;
            return TskCurationOutcome.Proposed(TskProposalMapper.ToResponse(proposal));
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

    /// <summary>
    /// Attaches 1–100 links of one source verse to the Elb1871 words <c>startPosition..endPosition</c> of that verse (roadmap 0024.5). Every key must be a
    /// link of the source (TSK row or added link, else 404); every position must exist in <c>Elb1871Words</c> and the quote (rebuilt from
    /// <c>WordInContext</c>, a client never sends one) must fit 500 characters (else 400). One transaction, one audit row per changed link; origin,
    /// rating and comment stay.
    /// </summary>
    public async Task<TskWordLinkOutcome> SaveWordLinkAsync(int userId, bool canWrite, SaveTskWordLinkRequest request, CancellationToken token = default)
    {
        if (!canWrite && _proposals is null)
        {
            return TskWordLinkOutcome.Refused(TskCurationStatus.Forbidden, ForbiddenMessage);
        }

        var pending = canWrite ? NoPending : await PendingOfSourceAsync(userId, request.SourceHebRefId, token);
        var (keys, added, missing) = await ResolveKeysAsync(request.SourceHebRefId, request.Keys, pending, token);
        if (missing is not null)
        {
            return TskWordLinkOutcome.Refused(TskCurationStatus.NotFound, missing);
        }

        var words = (await _repository.GetElbWordsAsync(request.SourceHebRefId, token))
            .Where(w => w.PositionInVerse >= request.StartPosition && w.PositionInVerse <= request.EndPosition)
            .OrderBy(w => w.PositionInVerse)
            .ToList();
        if (words.Count != request.EndPosition - request.StartPosition + 1)
        {
            return TskWordLinkOutcome.Refused(TskCurationStatus.BadRequest,
                $"The Elb1871 verse {request.SourceHebRefId} has no words at positions {request.StartPosition}..{request.EndPosition}.");
        }

        var quote = NoteQuote.Join(words.Select(w => w.WordInContext));
        if (quote.Length is 0 or > NoteQuote.MaxLength)
        {
            return TskWordLinkOutcome.Refused(TskCurationStatus.BadRequest, $"The quoted words must have 1 to {NoteQuote.MaxLength} characters.");
        }

        if (!canWrite)
        {
            return await ProposeManyAsync(userId, keys, pending,
                state => state with { StartPosition = request.StartPosition, EndPosition = request.EndPosition, QuotedText = quote }, token);
        }

        // An added link deleted meanwhile must not come back as a curated TSK link that TSK does not have: then it stays unchanged.
        var records = await _repository.ApplyManyAsync(keys,
            (key, state) => added.Contains(key) && state.Origin != TskOrigins.Added
                ? state
                : state with { StartPosition = request.StartPosition, EndPosition = request.EndPosition, QuotedText = quote },
            userId, TskCurationSources.Edit, token);

        return TskWordLinkOutcome.Ok(keys.Select((key, i) => ToResponse(key, records[i])).ToList());
    }

    /// <summary>Detaches 1–100 links of one source verse from their word (roadmap 0024.5); a TSK link that becomes plain loses its row. Links without a word link stay untouched (no audit row).</summary>
    public async Task<TskWordLinkOutcome> DeleteWordLinkAsync(int userId, bool canWrite, DeleteTskWordLinkRequest request, CancellationToken token = default)
    {
        if (!canWrite && _proposals is null)
        {
            return TskWordLinkOutcome.Refused(TskCurationStatus.Forbidden, ForbiddenMessage);
        }

        var pending = canWrite ? NoPending : await PendingOfSourceAsync(userId, request.SourceHebRefId, token);
        var (keys, _, missing) = await ResolveKeysAsync(request.SourceHebRefId, request.Keys, pending, token);
        if (missing is not null)
        {
            return TskWordLinkOutcome.Refused(TskCurationStatus.NotFound, missing);
        }

        if (!canWrite)
        {
            return await ProposeManyAsync(userId, keys, pending, state => state with { StartPosition = null, EndPosition = null, QuotedText = null }, token);
        }

        var records = await _repository.ApplyManyAsync(keys, (_, state) => state with { StartPosition = null, EndPosition = null, QuotedText = null },
            userId, TskCurationSources.Edit, token);

        return TskWordLinkOutcome.Ok(keys.Select((key, i) => ToResponse(key, records[i])).ToList());
    }

    /// <summary>The request keys as <see cref="TskLinkKey"/>s; <c>missing</c> names the first key that is no link of the source.</summary>
    /// <param name="pending">The proposer's pending proposals of the source: their own proposed added links count as links (InTsk false).</param>
    private async Task<(List<TskLinkKey> Keys, HashSet<TskLinkKey> Added, string? Missing)> ResolveKeysAsync(int source, IEnumerable<TskLinkTargetKey> targets,
        IReadOnlyDictionary<TskLinkKey, TskProposalRecord> pending, CancellationToken token)
    {
        var keys = targets.Select(k => new TskLinkKey(source, k.TargetStartHebRefId, k.TargetEndHebRefId)).ToList();
        var links = (await _repository.GetLinksOfSourceAsync(source, token)).ToDictionary(l => l.Key, l => l.InTsk);
        foreach (var proposed in pending.Values.Where(IsProposedLink))
        {
            links.TryAdd(proposed.Key, false);
        }

        var missing = keys.FirstOrDefault(k => !links.ContainsKey(k));
        if (missing is not null)
        {
            return (keys, [], $"Verse {source} has no TSK or added link to {missing.TargetStartHebRefId}"
                              + (missing.TargetEndHebRefId is { } end ? $"-{end}" : string.Empty) + ".");
        }

        // Keys that are only added links (an added key TSK also has counts as the TSK link).
        var added = keys.Where(k => !links[k]).ToHashSet();

        return (keys, added, null);
    }

    /// <summary>Non-maintainer path of <see cref="SaveAsync"/>: same 404 rule, plus the caller's own proposed added links count as links.</summary>
    private async Task<TskCurationOutcome> ProposeSaveAsync(int userId, SaveTskCurationRequest request, CancellationToken token)
    {
        var key = new TskLinkKey(request.SourceHebRefId, request.TargetStartHebRefId, request.TargetEndHebRefId);
        var current = await _repository.GetAsync(key, token);
        var pending = (await PendingOfSourceAsync(userId, key.SourceHebRefId, token)).GetValueOrDefault(key);
        if (current?.State.Origin != TskOrigins.Added && !(pending is not null && IsProposedLink(pending)) && !await _repository.TskLinkExistsAsync(key, token))
        {
            return TskCurationOutcome.NotFound("No TSK cross reference or added link with this source and target.");
        }

        var rating = string.IsNullOrWhiteSpace(request.Rating) ? null : request.Rating.Trim();
        var write = Desired(key, current, pending, state => state with { Rating = rating, Comment = NormaliseComment(request.Comment) });
        var proposal = (await _proposals!.ProposeAsync(userId, [write], token))[0];

        return proposal is null ? TskCurationOutcome.Ok(ToResponse(key, current)) : TskCurationOutcome.Proposed(TskProposalMapper.ToResponse(proposal));
    }

    /// <summary>Non-maintainer path of the word-link endpoints: one proposal per key whose desired state differs from the current one.</summary>
    private async Task<TskWordLinkOutcome> ProposeManyAsync(int userId, IReadOnlyList<TskLinkKey> keys, IReadOnlyDictionary<TskLinkKey, TskProposalRecord> pending,
        Func<TskCurationState, TskCurationState> change, CancellationToken token)
    {
        var writes = new List<TskProposalWrite>(keys.Count);
        foreach (var key in keys)
        {
            writes.Add(Desired(key, await _repository.GetAsync(key, token), pending.GetValueOrDefault(key), change));
        }

        var proposals = await _proposals!.ProposeAsync(userId, writes, token);
        return TskWordLinkOutcome.Proposed(proposals.OfType<TskProposalRecord>().Select(TskProposalMapper.ToResponse).ToList());
    }

    /// <summary>
    /// The desired state: <paramref name="change"/> applied to the caller's pending proposal when it was made on the current state (so a hide and a later
    /// word link of the same link end up in one proposal), otherwise to the current state. Equal to the current state = withdraw.
    /// </summary>
    public static TskProposalWrite Desired(TskLinkKey key, TskCurationRecord? current, TskProposalRecord? pending, Func<TskCurationState, TskCurationState> change)
    {
        var currentState = current?.State ?? TskCurationState.Plain;
        var baseState = pending is { Delete: false } && pending.BaseUpdatedAt == current?.UpdatedAt ? pending.State : currentState;
        var desired = change(baseState);
        if (desired.IsDefault)
        {
            desired = TskCurationState.Plain;
        }

        return new TskProposalWrite(key, desired, false, current?.UpdatedAt, desired == currentState);
    }

    private static bool IsProposedLink(TskProposalRecord proposal) => proposal is { Delete: false, State.Origin: TskOrigins.Added };

    private async Task<IReadOnlyDictionary<TskLinkKey, TskProposalRecord>> PendingOfSourceAsync(int userId, int source, CancellationToken token) =>
        (await _proposals!.GetPendingOfUserAsync(userId, source, source, token)).ToDictionary(p => p.Key);

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
        Word = record?.State is { StartPosition: { } from, EndPosition: { } to, QuotedText: { } quote }
            ? new TskWordResponse { StartPosition = from, EndPosition = to, QuotedText = quote, State = TskWordLinkStates.Ok }
            : null,
    };
}
