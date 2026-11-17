using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentReview.ListProposals;

[RequiresLogin]
public sealed class Endpoint : Endpoint<ListAgentProposalsRequest, ListAgentProposalsResponse>
{
    private readonly IAgentReviewService _service;

    public Endpoint(IAgentReviewService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("agent-runs/{runId}/proposals");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Proposals of an agent run in reading order (roadmap 0016.7). Query: status (open, merged, edited-merged, discarded, superseded), flaggedOnly.");
    }

    public override async Task HandleAsync(ListAgentProposalsRequest req, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        await Send.OkAsync(await _service.ListAsync(req.RunId, req.Status, req.FlaggedOnly, ct), ct);
    }
}

public sealed class Validator : Validator<ListAgentProposalsRequest>
{
    public Validator()
    {
        RuleFor(x => x.RunId).GreaterThan(0);
        RuleFor(x => x.Status).Must(s => string.IsNullOrEmpty(s) || AgentProposalStatuses.All.Contains(s)).WithMessage("status must be open, merged, edited-merged, discarded or superseded.");
    }
}
