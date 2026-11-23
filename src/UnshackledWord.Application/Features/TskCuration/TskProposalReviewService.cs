using System.Text.Json;
using FluentValidation;
using UnshackledWord.Application.Features.UserAdmin;
using UnshackledWord.Domain.WebApi.Tsk;

namespace UnshackledWord.Application.Features.TskCuration;

/// <summary>Contract shapes of <see cref="TskProposalRecord"/> (roadmap 0024.6).</summary>
public static class TskProposalMapper
{
    public static TskProposalResponse ToResponse(TskProposalRecord proposal) => new()
    {
        Id = proposal.Id,
        SourceHebRefId = proposal.Key.SourceHebRefId,
        TargetStartHebRefId = proposal.Key.TargetStartHebRefId,
        TargetEndHebRefId = proposal.Key.TargetEndHebRefId,
        State = proposal.ProposalState,
        Proposed = ToProposed(proposal),
        CreatedAt = proposal.CreatedAt,
        DecidedAt = proposal.DecidedAt,
        Reason = proposal.Reason,
    };

    public static TskProposedStateResponse ToProposed(TskProposalRecord proposal) => new()
    {
        Origin = proposal.State.Origin,
        Rating = proposal.State.Rating,
        Comment = proposal.State.Comment,
        Word = Word(proposal.State),
        Delete = proposal.Delete ? true : null,
    };

    public static TskPendingProposalResponse ToPending(TskProposalRecord proposal) => new()
    {
        Id = proposal.Id,
        Rating = proposal.State.Rating,
        Comment = proposal.State.Comment,
        Word = Word(proposal.State),
        Delete = proposal.Delete ? true : null,
    };

    public static TskWordResponse? Word(TskCurationState state) => state is { StartPosition: { } from, EndPosition: { } to, QuotedText: { } quote }
        ? new TskWordResponse { StartPosition = from, EndPosition = to, QuotedText = quote, State = TskWordLinkStates.Ok }
        : null;
}

public enum TskProposalReviewStatus
{
    Ok,
    NotFound,

    /// <summary>Not pending, or (accept) stale without <c>force</c>: HTTP 400.</summary>
    Invalid,
}

public sealed record TskProposalReviewResult(TskProposalReviewStatus Status, string? Message = null, TskProposalResponse? Proposal = null,
    TskCurationResponse? Curation = null);

/// <summary>
/// Maintainer review of TSK curation proposals (roadmap 0024.6): list, accept (stale-base check, applied through the curation write path with audit
/// <c>Source</c> <c>proposal</c>), reject with reason. Both decisions append to the admin log (actor = maintainer, target = author,
/// details <c>{ proposalId, key: { sourceHebRefId, targetStartHebRefId, targetEndHebRefId } }</c>), like the note review of 0023.5.
/// Also the author's own list.
/// </summary>
public sealed class TskProposalReviewService
{
    public const int MaxReviewItems = 1000;
    public const int MaxOwnItems = 200;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ITskCurationProposalRepository _repository;
    private readonly IAdminLogRepository _adminLog;

    public TskProposalReviewService(ITskCurationProposalRepository repository, IAdminLogRepository adminLog)
    {
        _repository = repository;
        _adminLog = adminLog;
    }

    public async Task<TskProposalReviewListResponse> ListPendingAsync(CancellationToken token = default)
    {
        var rows = await _repository.GetPendingAsync(MaxReviewItems, token);

        return new TskProposalReviewListResponse
        {
            Proposals = rows.Select(r => new TskProposalReviewItemResponse
            {
                Id = r.Proposal.Id,
                SourceHebRefId = r.Proposal.Key.SourceHebRefId,
                TargetStartHebRefId = r.Proposal.Key.TargetStartHebRefId,
                TargetEndHebRefId = r.Proposal.Key.TargetEndHebRefId,
                InTsk = r.InTsk,
                Current = r.Current is { } current
                    ? new TskCurrentStateResponse
                    {
                        Origin = current.State.Origin, Rating = current.State.Rating, Comment = current.State.Comment,
                        Word = TskProposalMapper.Word(current.State), UpdatedAt = current.UpdatedAt,
                    }
                    : null,
                Proposed = TskProposalMapper.ToProposed(r.Proposal),
                AuthorUserId = r.Proposal.UserId,
                AuthorDisplayName = r.AuthorDisplayName,
                CreatedAt = r.Proposal.CreatedAt,
                Stale = r.Stale,
            }).ToList(),
        };
    }

    public async Task<MyTskProposalsResponse> ListMineAsync(int userId, CancellationToken token = default) => new()
    {
        Proposals = (await _repository.GetOfUserAsync(userId, MaxOwnItems, token)).Select(TskProposalMapper.ToResponse).ToList(),
    };

    public async Task<TskProposalReviewResult> AcceptAsync(int actorId, int id, bool force, CancellationToken token = default)
    {
        var decision = await _repository.AcceptAsync(id, actorId, force, token);
        var refused = Refused(decision, "The link changed since the proposal was made; check the current state and accept with force to apply it anyway.");
        if (refused is not null)
        {
            return refused;
        }

        await LogAsync(actorId, AdminLogActions.TskProposalAccepted, decision.Proposal!, token);

        return new TskProposalReviewResult(TskProposalReviewStatus.Ok, null, TskProposalMapper.ToResponse(decision.Proposal!),
            TskCurationService.ToResponse(decision.Proposal!.Key, decision.Curation));
    }

    public async Task<TskProposalReviewResult> RejectAsync(int actorId, int id, string? reason, CancellationToken token = default)
    {
        var cleanReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        var decision = await _repository.RejectAsync(id, actorId, cleanReason, token);
        var refused = Refused(decision, null);
        if (refused is not null)
        {
            return refused;
        }

        await LogAsync(actorId, AdminLogActions.TskProposalRejected, decision.Proposal!, token);

        return new TskProposalReviewResult(TskProposalReviewStatus.Ok, null, TskProposalMapper.ToResponse(decision.Proposal!));
    }

    private static TskProposalReviewResult? Refused(TskProposalDecision decision, string? staleMessage) => decision.Status switch
    {
        TskProposalDecisionStatus.NotFound => new TskProposalReviewResult(TskProposalReviewStatus.NotFound, "No such proposal."),
        TskProposalDecisionStatus.NotPending => new TskProposalReviewResult(TskProposalReviewStatus.Invalid,
            $"The proposal is not pending (state {decision.Proposal?.ProposalState})."),
        TskProposalDecisionStatus.Stale => new TskProposalReviewResult(TskProposalReviewStatus.Invalid, staleMessage),
        _ => null,
    };

    private Task LogAsync(int actorId, string action, TskProposalRecord proposal, CancellationToken token)
    {
        var details = JsonSerializer.Serialize(new
        {
            proposalId = proposal.Id,
            key = new { proposal.Key.SourceHebRefId, proposal.Key.TargetStartHebRefId, proposal.Key.TargetEndHebRefId },
        }, Json);

        return _adminLog.AppendAsync(new AdminLogEntry(actorId, action, proposal.UserId, null, details), null, token);
    }
}

/// <summary>Shape of <see cref="RejectTskProposalRequest"/>: reason at most 500 characters after trimming.</summary>
public sealed class RejectTskProposalRequestValidator : AbstractValidator<RejectTskProposalRequest>
{
    public const int MaxReasonLength = 500;

    public RejectTskProposalRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Reason)
            .Must(reason => reason is null || reason.Trim().Length <= MaxReasonLength)
            .WithMessage($"reason must have at most {MaxReasonLength} characters.");
    }
}

/// <summary>Shape of <see cref="AcceptTskProposalRequest"/>.</summary>
public sealed class AcceptTskProposalRequestValidator : AbstractValidator<AcceptTskProposalRequest>
{
    public AcceptTskProposalRequestValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}
