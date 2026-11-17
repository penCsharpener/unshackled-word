using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentReview.BulkMerge;

[RequiresLogin]
public sealed class Endpoint : Endpoint<BulkMergeAgentProposalsRequest, BulkMergeAgentProposalsResponse>
{
    private readonly IAgentReviewService _service;

    public Endpoint(IAgentReviewService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("agent-runs/{runId}/proposals/merge-unflagged");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Merges every open proposal of the run that has no flag (author: the AI user, reviewer: the caller). Needs confirm=true in the body. Stale or invalid verses stay open and come back in failed; flagged ones are only counted.");
    }

    public override async Task HandleAsync(BulkMergeAgentProposalsRequest req, CancellationToken ct)
    {
        var reviewer = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _service.BulkMergeUnflaggedAsync(req.RunId, reviewer, ct);
        if (result.Outcome != AgentReviewOutcome.Ok)
        {
            foreach (var error in result.Errors)
            {
                AddError(error);
            }

            await Send.ErrorsAsync(ReviewHttp.StatusOf(result.Outcome), ct);
            return;
        }

        await Send.OkAsync(result.Value!, ct);
    }
}

public sealed class Validator : Validator<BulkMergeAgentProposalsRequest>
{
    public Validator()
    {
        RuleFor(x => x.RunId).GreaterThan(0);
        RuleFor(x => x.Confirm).Equal(true).WithMessage("Set confirm to true: this merges every unflagged proposal of the run into the live mapping.");
    }
}
