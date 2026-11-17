using FastEndpoints;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentRuns.Cancel;

[RequiresLogin]
public sealed class Endpoint : Endpoint<AgentRunIdRequest, AgentRunDto>
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
        Post("agent-runs/{runId}/cancel");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Cancels a queued or running agent run: no new package is started, the package in flight finishes (its tokens are still counted). Stored proposals stay. 409 when the run is already finished.");
    }

    public override async Task HandleAsync(AgentRunIdRequest req, CancellationToken ct)
    {
        var result = await _service.CancelAsync(req.RunId, ct);
        if (result.Outcome != AgentRunPrepareOutcome.Ok)
        {
            AddError(result.Error ?? "Could not cancel the run.");
            await Send.ErrorsAsync(AgentRunResponses.StatusOf(result.Outcome), ct);

            return;
        }

        await Send.OkAsync(AgentRunMapper.ToDto((await _runs.GetRunAsync(req.RunId, ct))!, _manager.IsActive(req.RunId)), ct);
    }
}
