using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentReview.Merge;

[RequiresLogin]
public sealed class Endpoint : Endpoint<AgentProposalIdRequest, MergeAgentProposalResponse>
{
    private readonly IAgentReviewService _service;

    public Endpoint(IAgentReviewService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("agent-proposals/{id}/merge");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Merges an open proposal into the live mapping and lemmas: rows are written with the AI user as author, the caller is stored as reviewer. 409 when the proposal is not open or the verse changed since (veraltet), 400 when a hard rule fails against the live verse, 403 while mapping editing is off.");
    }

    public override async Task HandleAsync(AgentProposalIdRequest req, CancellationToken ct)
    {
        var reviewer = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _service.MergeAsync(req.Id, reviewer, ct);
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
