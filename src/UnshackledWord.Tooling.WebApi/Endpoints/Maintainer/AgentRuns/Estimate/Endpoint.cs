using FluentValidation;
using FastEndpoints;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentRuns.Estimate;

[RequiresLogin]
public sealed class Endpoint : Endpoint<EstimateAgentRunRequest, AgentRunEstimateResponse>
{
    private readonly IAgentRunService _service;

    public Endpoint(IAgentRunService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("agent-runs/estimate");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Estimates tokens and cost of an agent run over a verse range with a provider profile (07-providers-and-caching.md, Budget): packages, input/cached/output tokens with a 30 % retry margin, cost from the profile prices, and whether it would pass the cap. Calls no model and stores nothing.");
    }

    public override async Task HandleAsync(EstimateAgentRunRequest req, CancellationToken ct)
    {
        var result = await _service.EstimateAsync(req, ct);
        if (result.Estimate is null)
        {
            AddError(result.Error ?? "Estimate failed.");
            await Send.ErrorsAsync(AgentRunResponses.StatusOf(result.Outcome), ct);

            return;
        }

        await Send.OkAsync(result.Estimate, ct);
    }
}

public sealed class Validator : Validator<EstimateAgentRunRequest>
{
    public Validator()
    {
        Include(new AgentRunRangeValidator());
    }
}
