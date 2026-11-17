using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentReview.Discard;

[RequiresLogin]
public sealed class Endpoint : Endpoint<AgentProposalIdRequest, DiscardAgentProposalResponse>
{
    private readonly IAgentReviewService _service;

    public Endpoint(IAgentReviewService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("agent-proposals/{id}/discard");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Discards an open proposal; the live data is not touched. 409 when it is not open.");
    }

    public override async Task HandleAsync(AgentProposalIdRequest req, CancellationToken ct)
    {
        var reviewer = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _service.DiscardAsync(req.Id, reviewer, ct);
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
