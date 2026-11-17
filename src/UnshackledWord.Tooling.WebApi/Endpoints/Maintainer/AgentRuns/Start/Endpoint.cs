using FluentValidation;
using System.Security.Claims;
using FastEndpoints;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentRuns.Start;

[RequiresLogin]
public sealed class Endpoint : Endpoint<StartAgentRunRequest, AgentRunDto>
{
    private readonly IAgentRunService _service;
    private readonly IAgentRunRepository _runs;
    private readonly AgentRunManager _manager;
    private readonly LinkRoles.LinkRoleJobRunner _linkRoles;

    public Endpoint(IAgentRunService service, IAgentRunRepository runs, AgentRunManager manager, LinkRoles.LinkRoleJobRunner linkRoles)
    {
        _service = service;
        _runs = runs;
        _manager = manager;
        _linkRoles = linkRoles;
    }

    public override void Configure()
    {
        Post("agent-runs");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Starts an agent run in the background (range, profile, model, mode; optional maxTokens/maxCostUsd override the profile caps) and returns it with status 202; poll GET maintainer/agent-runs/{runId}. Proposals only, no live data is written. 400 for an unusable profile (no API key) or bad range, 404 when the range has no German words.");
    }

    public override async Task HandleAsync(StartAgentRunRequest req, CancellationToken ct)
    {
        if (_linkRoles.IsRunning)
        {
            // Roadmap 0018.4: the link-roles backfill rewrites the mapping tables; no run starts while it works.
            AddError("The link-roles backfill is running; start the agent run when it has finished.");
            await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);

            return;
        }

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var created = await _service.CreateRunAsync(req, userId, ct);
        if (created.Outcome != AgentRunPrepareOutcome.Ok)
        {
            AddError(created.Error ?? "Could not start the run.");
            await Send.ErrorsAsync(AgentRunResponses.StatusOf(created.Outcome), ct);

            return;
        }

        _manager.TryLaunch(created.RunId);
        var run = await _runs.GetRunAsync(created.RunId, ct);

        await Send.ResponseAsync(AgentRunMapper.ToDto(run!, true), StatusCodes.Status202Accepted, ct);
    }
}

public sealed class Validator : Validator<StartAgentRunRequest>
{
    public Validator()
    {
        Include(new AgentRunRangeValidator());
        RuleFor(x => x.MaxTokens).GreaterThan(0).When(x => x.MaxTokens is not null);
        RuleFor(x => x.MaxCostUsd).GreaterThan(0).When(x => x.MaxCostUsd is not null);
    }
}
