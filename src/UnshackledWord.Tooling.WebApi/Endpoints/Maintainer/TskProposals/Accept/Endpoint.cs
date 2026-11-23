using System.Security.Claims;
using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.TskCuration;
using UnshackledWord.Domain.WebApi.Tsk;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.TskProposals.Accept;

/// <summary>Maintainer accepts a TSK curation proposal (roadmap 0024.6): applied with audit <c>Source</c> <c>proposal</c>, logged in the admin log.</summary>
[RequiresLogin]
public sealed class Endpoint : Endpoint<AcceptTskProposalRequest, AcceptTskProposalResponse>
{
    private readonly TskProposalReviewService _service;

    public Endpoint(TskProposalReviewService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("tsk/proposals/{id}/accept");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Description(b => b.Produces<AcceptTskProposalResponse>(200).ProducesProblem(400).ProducesProblem(404));
        Summary(s =>
        {
            s.Summary = "Maintainer: applies the proposal's desired state to the link (audit `Source` `proposal`, `UserId` = maintainer, `proposalId` in `After`) "
                        + "and marks it accepted. Body optional: `{ \"force\": true }` applies a stale proposal anyway. Returns the proposal and the resulting curation.";
            s[400] = "The proposal is not pending, or it is stale (the link changed since it was made) and `force` is not set; nothing changes.";
            s[404] = "No such proposal.";
        });
    }

    public override async Task HandleAsync(AcceptTskProposalRequest req, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        var actorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _service.AcceptAsync(actorId, req.Id, req.Force, ct);

        if (result.Status == TskProposalReviewStatus.Ok)
        {
            await Send.OkAsync(new AcceptTskProposalResponse { Proposal = result.Proposal!, Curation = result.Curation! }, ct);
            return;
        }

        AddError(result.Message ?? "The request was refused.");
        await Send.ErrorsAsync(result.Status == TskProposalReviewStatus.NotFound ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest, ct);
    }
}

public sealed class Validator : Validator<AcceptTskProposalRequest>
{
    public Validator()
    {
        Include(new AcceptTskProposalRequestValidator());
    }
}
