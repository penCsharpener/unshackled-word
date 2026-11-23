using System.Security.Claims;
using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.TskCuration;
using UnshackledWord.Domain.WebApi.Tsk;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.TskProposals.Reject;

/// <summary>Maintainer rejects a TSK curation proposal (roadmap 0024.6) with an optional reason the author sees; logged in the admin log.</summary>
[RequiresLogin]
public sealed class Endpoint : Endpoint<RejectTskProposalRequest, TskProposalResponse>
{
    private readonly TskProposalReviewService _service;

    public Endpoint(TskProposalReviewService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("tsk/proposals/{id}/reject");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Description(b => b.Produces<TskProposalResponse>(200).ProducesProblem(400).ProducesProblem(404));
        Summary(s =>
        {
            s.Summary = "Maintainer: rejects a pending proposal; body optional `{ \"reason\": \"…\" }` (max 500 characters, trimmed, empty = none). "
                        + "The curation is untouched. Returns the proposal (state `rejected`).";
            s[400] = "The proposal is not pending, or the reason is too long.";
            s[404] = "No such proposal.";
        });
    }

    public override async Task HandleAsync(RejectTskProposalRequest req, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        var actorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _service.RejectAsync(actorId, req.Id, req.Reason, ct);

        if (result.Status == TskProposalReviewStatus.Ok)
        {
            await Send.OkAsync(result.Proposal!, ct);
            return;
        }

        AddError(result.Message ?? "The request was refused.");
        await Send.ErrorsAsync(result.Status == TskProposalReviewStatus.NotFound ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest, ct);
    }
}

public sealed class Validator : Validator<RejectTskProposalRequest>
{
    public Validator()
    {
        Include(new RejectTskProposalRequestValidator());
    }
}
