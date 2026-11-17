using FluentValidation;
using FastEndpoints;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentRuns.Resume;

[RequiresLogin]
public sealed class Endpoint : Endpoint<ResumeAgentRunRequest, AgentRunDto>
{
    private readonly IAgentRunService _service;
    private readonly IAgentRunRepository _runs;
    private readonly AgentRunManager _manager;

    public Endpoint(IAgentRunService service, IAgentRunRepository runs, AgentRunManager manager)
    {
        _service = service;
        _runs = runs;
        _manager = manager;
    }

    public override void Configure()
    {
        Post("agent-runs/{runId}/resume");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Continues a failed, cancelled or budget-exceeded run, optionally with a raised maxTokens/maxCostUsd (must exceed what is spent). Verses that already have a proposal in the run are not asked again. 409 for a run that is active or completed.");
    }

    public override async Task HandleAsync(ResumeAgentRunRequest req, CancellationToken ct)
    {
        var result = await _service.PrepareResumeAsync(req, _manager.IsActive(req.RunId), ct);
        if (result.Outcome != AgentRunPrepareOutcome.Ok)
        {
            AddError(result.Error ?? "Could not resume the run.");
            await Send.ErrorsAsync(AgentRunResponses.StatusOf(result.Outcome), ct);

            return;
        }

        _manager.TryLaunch(result.RunId);

        await Send.ResponseAsync(AgentRunMapper.ToDto((await _runs.GetRunAsync(result.RunId, ct))!, true), StatusCodes.Status202Accepted, ct);
    }
}

public sealed class Validator : Validator<ResumeAgentRunRequest>
{
    public Validator()
    {
        RuleFor(x => x.RunId).GreaterThan(0);
        RuleFor(x => x.MaxTokens).GreaterThan(0).When(x => x.MaxTokens is not null);
        RuleFor(x => x.MaxCostUsd).GreaterThan(0).When(x => x.MaxCostUsd is not null);
    }
}
